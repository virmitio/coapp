//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Client {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Common.Exceptions;
    using Common.Model;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Pipes;
    using Toolkit.Tasks;
    using Toolkit.Win32;

    public class CallResponse : IPackageManagerResponse {
        private static readonly IPackageManager PM = PackageManager.RemoteService;

        private readonly Lazy<List<Package>> _packages = new Lazy<List<Package>>(() => new List<Package>());
        private readonly Lazy<List<Feed>> _feeds = new Lazy<List<Feed>>(() => new List<Feed>());
        private readonly Lazy<List<Policy>> _policies = new Lazy<List<Policy>>(() => new List<Policy>());
        private readonly Lazy<List<ScheduledTask>> _scheduledTasks = new Lazy<List<ScheduledTask>>(() => new List<ScheduledTask>());

        private readonly IncomingCallDispatcher<IPackageManagerResponse> _dispatcher;

        internal LoggingSettings LoggingSettingsResult;
        internal bool EngineRestarting;
        internal bool NoPackages;
        internal bool IsSignatureValid;
        internal bool OptedIn;

        internal IEnumerable<Package> Packages {
            get {
                return _packages.IsValueCreated ? _packages.Value.Distinct() : Enumerable.Empty<Package>();
            }
        }

        internal IEnumerable<Feed> Feeds {
            get {
                return _feeds.IsValueCreated ? _feeds.Value.Distinct() : Enumerable.Empty<Feed>();
            }
        }

        internal IEnumerable<Policy> Policies {
            get {
                return _policies.IsValueCreated ? _policies.Value.Distinct() : Enumerable.Empty<Policy>();
            }
        }

        internal IEnumerable<ScheduledTask> ScheduledTasks {
            get {
                return _scheduledTasks.IsValueCreated ? _scheduledTasks.Value.Distinct() : Enumerable.Empty<ScheduledTask>();
            }
        }

        internal string OperationCanceledReason;
        internal Package UpgradablePackage;
        internal IEnumerable<Package> PotentialUpgrades;
        internal static Dictionary<string, Task> CurrentDownloads = new Dictionary<string, Task>();

        public CallResponse() {
            // this makes sure that all response messages are getting sent back to here.
            _dispatcher = new IncomingCallDispatcher<IPackageManagerResponse>(this);
            CurrentTask.Events += new GetResponseDispatcher(() => _dispatcher);
        }

        internal void Clear() {
            EngineRestarting = false;
            NoPackages = false;
            IsSignatureValid = false;
            OperationCanceledReason = null;
        }

        public void ThrowWhenFaulted(Task antecedent) {
            // do not get all fussy when the engine is restarting.
            if (EngineRestarting) {
                return;
            }

            if (!string.IsNullOrEmpty(OperationCanceledReason)) {
                throw new OperationCanceledException(OperationCanceledReason);
            }

            antecedent.RethrowWhenFaulted();
        }

        public void NoPackagesFound() {
            NoPackages = true;
        }

        public void PolicyInformation(string name, string description, IEnumerable<string> accounts) {
            _policies.Value.Add(new Policy {Name = name, Description = description, Members = accounts});
        }

        public void SendSessionStarted(string sessionId) {
            // throw new NotImplementedException();
        }

        public void PackageInformation(CanonicalName canonicalName, string localLocation, bool installed, bool blocked, bool required, bool clientRequired, bool active, bool dependent, FourPartVersion minPolicy, FourPartVersion maxPolicy,
            IEnumerable<string> remoteLocations, IEnumerable<CanonicalName> dependencies, IEnumerable<CanonicalName> supercedentPackages) {
            if (!Environment.Is64BitOperatingSystem && canonicalName.Architecture == Architecture.x64) {
                // skip x64 packages from the result set if you're not on an x64 system.
                return;
            }

            var result = Package.GetPackage(canonicalName);
            result.LocalPackagePath = localLocation;

            result.MinPolicy = minPolicy;
            result.MaxPolicy = maxPolicy;

            // result.ProductCode = prod
            result.IsInstalled = installed;
            result.IsBlocked = blocked;
            result.IsRequired = required;
            result.IsClientRequired = clientRequired;
            result.IsActive = active;
            result.IsDependency = dependent;
            result.RemoteLocations = remoteLocations;
            result.Dependencies = dependencies;
            result.SupercedentPackages = supercedentPackages;
            result.IsPackageInfoStale = false;
            _packages.Value.Add(result);
        }

        public void PackageDetails(CanonicalName canonicalName, Dictionary<string, string> metadata, IEnumerable<string> iconLocations, Dictionary<string, string> licenses, Dictionary<string, string> roles, IEnumerable<string> tags,
            IDictionary<string, string> contributorUrls, IDictionary<string, string> contributorEmails) {
            var result = Package.GetPackage(canonicalName);
            result.Icon = iconLocations.FirstOrDefault();
            result.Roles = roles.Keys.Select(each => new Role {Name = each, PackageRole = (PackageRole)typeof (PackageRole).ParseString(roles[each])}); //? is this right?
            result.Tags = tags;
            // licenses not done yet.
            result.Description = metadata["description"];
            result.Summary = metadata["summary"];
            result.Summary = metadata["summary"];
            result.DisplayName = metadata["display-name"];
            result.Copyright = metadata["copyright"];
            result.AuthorVersion = metadata["author-version"];
            result.IsPackageDetailsStale = false;
        }

        public void FeedDetails(string location, DateTime lastScanned, bool session, bool suppressed, bool validated, string state) {
            _feeds.Value.Add(new Feed {
                Location = location,
                LastScanned = lastScanned,
                IsSession = session,
                IsSuppressed = suppressed,
                FeedState = state.ParseEnum(FeedState.active)
            });
        }

        public void InstallingPackageProgress(CanonicalName canonicalName, int percentComplete, int overallProgress) {
            Event<PackageInstallProgress>.Raise(canonicalName, percentComplete, overallProgress);
        }

        public void RemovingPackageProgress(CanonicalName canonicalName, int percentComplete) {
            Event<PackageRemoveProgress>.Raise(canonicalName, percentComplete);
        }

        public void InstalledPackage(CanonicalName canonicalName) {
            _packages.Value.Add(Package.GetPackage(canonicalName));
            Package.GetPackage(canonicalName).IsInstalled = true;
            Event<PackageInstalled>.Raise(canonicalName);
        }

        public void RemovedPackage(CanonicalName canonicalName) {
            _packages.Value.Add(Package.GetPackage(canonicalName));
            Package.GetPackage(canonicalName).IsInstalled = false;
            Event<PackageRemoved>.Raise(canonicalName);
        }

        public void FailedPackageInstall(CanonicalName canonicalName, string filename, string reason) {
            throw new CoAppException("Package Failed Install {0} => {1}".format(canonicalName, reason));
        }

        public void FailedPackageRemoval(CanonicalName canonicalName, string reason) {
            throw new FailedPackageRemoveException(canonicalName, reason);
        }

        public void RequireRemoteFile(CanonicalName canonicalName, IEnumerable<string> remoteLocations, string destination, bool force) {
            var targetFilename = Path.Combine(destination, canonicalName);
            lock (CurrentDownloads) {
                if (CurrentDownloads.ContainsKey(targetFilename)) {
                    // wait for this guy to respond (which should give us what we need)
                    CurrentDownloads[targetFilename].Continue(() => {
                        if (File.Exists(targetFilename)) {
                            Event<DownloadCompleted>.Raise(canonicalName, targetFilename);
                            PM.RecognizeFile(canonicalName, targetFilename, remoteLocations.FirstOrDefault());
                        }
                        return;
                    });
                    return;
                }

                // gotta download the file...
                var task = Task.Factory.StartNew(() => {
                    foreach (var location in remoteLocations) {
                        try {
                            // a filesystem location (remote or otherwise)
                            var uri = new Uri(location);
                            if (uri.IsFile) {
                                // try to copy the file local.
                                var remoteFile = uri.AbsoluteUri.CanonicalizePath();

                                // if this fails, we'll just move down the line.
                                File.Copy(remoteFile, targetFilename);
                                PM.RecognizeFile(canonicalName, targetFilename, uri.AbsoluteUri);
                                return;
                            }

                            // A web location
                            Task progressTask = null;
                            var success = false;
                            var rf = new RemoteFile(uri, targetFilename,
                                completed: (itemUri) => {
                                    PM.RecognizeFile(canonicalName, targetFilename, uri.AbsoluteUri);
                                    Event<DownloadCompleted>.Raise(canonicalName, targetFilename);
                                    // remove it from the list of current downloads
                                    CurrentDownloads.Remove(targetFilename);
                                    success = true;
                                },
                                failed: (itemUri) => {
                                    success = false;
                                },
                                progress: (itemUri, percent) => {
                                    if (progressTask == null) {
                                        // report progress to the engine
                                        progressTask = PM.DownloadProgress(canonicalName, percent);
                                        progressTask.Continue(() => {
                                            progressTask = null;
                                        });
                                    }

                                    Event<DownloadProgress>.Raise(canonicalName, targetFilename, percent);
                                });

                            rf.Get();

                            if (success && File.Exists(targetFilename)) {
                                return;
                            }
                        } catch (Exception e) {
                            // bogus, dude.
                            // try the next one.
                            Logger.Error(e);
                        }
                        // loop around and try again?
                    }

                    // was there a file there from before?
                    if (File.Exists(targetFilename)) {
                        Event<DownloadCompleted>.Raise(canonicalName, targetFilename);
                        PM.RecognizeFile(canonicalName, targetFilename, remoteLocations.FirstOrDefault());
                    }

                    // remove it from the list of current downloads
                    CurrentDownloads.Remove(targetFilename);

                    // if we got here, that means we couldn't get the file. too bad, so sad.
                    PM.UnableToAcquire(canonicalName);
                }, TaskCreationOptions.AttachedToParent);

                CurrentDownloads.Add(targetFilename, task);
            }
        }

        public void SignatureValidation(string filename, bool isValid, string certificateSubjectName) {
            IsSignatureValid = isValid;
        }

        public void PermissionRequired(string policyRequired) {
            throw new RequiresPermissionException(policyRequired);
        }

        public void Error(string messageName, string argumentName, string problem) {
            if (messageName == "AddFeed") {
                throw new CoAppException(problem);
            }
            throw new CoAppException("Message Argument Exception [{0}/{1}/{2}]".format(messageName, argumentName, problem));
        }

        public void Warning(string messageName, string argumentName, string problem) {
            // throw new NotImplementedException();
        }

        public void PackageSatisfiedBy(string requestedCanonicalName, string satisfiedByCanonicalName) {
            var pkg = Package.GetPackage(requestedCanonicalName);
            pkg.SatisfiedBy = Package.GetPackage(satisfiedByCanonicalName);
            _packages.Value.Add(pkg);
        }

        public void FeedAdded(string location) {
            _feeds.Value.Add(new Feed {Location = location});
        }

        public void FeedRemoved(string location) {
            _feeds.Value.Add(new Feed {Location = location});
        }

        public void FileNotFound(string filename) {
            throw new NotImplementedException();
        }

        public void UnknownPackage(CanonicalName canonicalName) {
            throw new UnknownPackageException(canonicalName);
        }

        public void PackageBlocked(CanonicalName canonicalName) {
            throw new PackageBlockedException(canonicalName);
        }

        public void FileNotRecognized(string filename, string reason) {
            throw new NotImplementedException();
        }

        public void UnexpectedFailure(string type, string failure, string stacktrace) {
            throw new CoAppException(failure);
        }

        public void FeedSuppressed(string location) {
            _feeds.Value.Add(new Feed {Location = location});
        }

        public void SendKeepAlive() {
            throw new NotImplementedException();
        }

        public void OperationCanceled(string message) {
            OperationCanceledReason = message;
        }

        public void PackageHasPotentialUpgrades(CanonicalName packageCanonicalName, IEnumerable<CanonicalName> supercedents) {
            UpgradablePackage = Package.GetPackage(packageCanonicalName);
            PotentialUpgrades = supercedents.Select(Package.GetPackage);
        }

        public void ScheduledTaskInfo(string taskName, string executable, string commandline, int hour, int minutes, DayOfWeek? dayOfWeek, int intervalInMinutes) {
            _scheduledTasks.Value.Add(new ScheduledTask {
                Name = taskName,
                Executable = executable,
                CommandLine = commandline,
                Hour = hour,
                Minutes = minutes,
                DayOfWeek = dayOfWeek,
                IntervalInMinutes = intervalInMinutes
            });
        }

        public void CurrentTelemetryOption(bool optIntoTelemetryTracking) {
            OptedIn = optIntoTelemetryTracking;
        }

        public void NoFeedsFound() {
            // throw new NotImplementedException();
        }

        public void Restarting() {
            EngineRestarting = true;
            // throw an exception here to quickly short circuit the rest of this call
            throw new Exception("restarting");
        }

        public void SendShuttingDown() {
            // nothing to do here but smile!
        }

        public void UnableToDownloadPackage(CanonicalName packageCanonicalName) {
            Event<UnableToDownloadPackage>.Raise(packageCanonicalName);
        }

        public void UnableToInstallPackage(CanonicalName packageCanonicalName) {
            throw new PackageInstallFailedException(Package.GetPackage(packageCanonicalName));
        }

        public void Recognized(string location) {
            // nothing to do here but smile!
        }

        public void TaskComplete() {
            // nothing to do here but smile!
        }

        public void LoggingSettings(bool messages, bool warnings, bool errors) {
            LoggingSettingsResult = new LoggingSettings {Messages = messages, Warnings = warnings, Errors = errors};
        }
    }
}