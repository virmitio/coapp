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

namespace CoApp.Packaging.Service {
    using System.Linq;
    using Common;
    using Toolkit.Configuration;
    using Toolkit.Crypto;
    using Toolkit.Extensions;
    using Toolkit.Tasks;

    /// <summary>
    ///   This stores information that is really only relevant to the currently running Session, not between sessions. The instance of this is bound to the Session.
    /// </summary>
    internal class PackageSessionData : NotifiesPackageManager {
        internal bool DoNotSupercede; // TODO: it's possible these could be contradictory
        internal bool UpgradeAsNeeded; // TODO: it's possible these could be contradictory
        internal bool IsClientSpecified;
        internal bool HasRequestedDownload;

        internal bool IsDependency;

        private bool _couldNotDownload;
        private Package _supercedent;
        private bool _packageFailedInstall;
        private readonly Package _package;
        private string _localValidatedLocation;

        internal PackageSessionData(Package package) {
            _package = package;
        }

        public Package Supercedent {
            get {
                return _supercedent;
            }
            set {
                if (value != _supercedent) {
                    _supercedent = value;
                    Changed();
                }
            }
        }

        public bool PackageFailedInstall {
            get {
                return _packageFailedInstall;
            }
            set {
                if (_packageFailedInstall != value) {
                    _packageFailedInstall = value;
                    Changed();
                }
            }
        }

        /*
        public bool Supercedes(Package p)
        {
            return Architecture == p.Architecture &&
                   PublicKeyToken == p.PublicKeyToken &&
                   Name.Equals(p.Name, StringComparison.CurrentCultureIgnoreCase) &&
                   p.Version <= PolicyMaximumVersion && p.Version >= PolicyMinimumVersion;
        }
        */

        public bool CouldNotDownload {
            get {
                return _couldNotDownload;
            }
            set {
                if (value != _couldNotDownload) {
                    _couldNotDownload = value;
                    Changed();
                }
            }
        }

        public bool AllowedToSupercede {
            get {
                return UpgradeAsNeeded || (!IsClientSpecified && !DoNotSupercede) && IsPotentiallyInstallable;
            }
        }

        public bool IsPotentiallyInstallable {
            get {
                return !PackageFailedInstall && (_package.InternalPackageData.HasLocalLocation || !CouldNotDownload && _package.InternalPackageData.HasRemoteLocation);
            }
        }

        public bool CanSatisfy { get; set; }

        public string LocalValidatedLocation {
            get {
                var remoteInterface = Event<GetResponseInterface>.RaiseFirst();

                if (!string.IsNullOrEmpty(_localValidatedLocation) && _localValidatedLocation.FileIsLocalAndExists()) {
                    return _localValidatedLocation;
                }

                var location = _package.InternalPackageData.LocalLocation;
                if (string.IsNullOrEmpty(location)) {
                    // there are no local locations at all for this package?
                    return _localValidatedLocation = null;
                }

                if (Verifier.HasValidSignature(location)) {
                    if (remoteInterface != null) {
                        Event<GetResponseInterface>.RaiseFirst().SignatureValidation(location, true, Verifier.GetPublisherName(location));
                    }
                    return _localValidatedLocation = location;
                }
                if (remoteInterface != null) {
                    Event<GetResponseInterface>.RaiseFirst().SignatureValidation(location, false, null);
                }
                var result = _package.InternalPackageData.LocalLocations.Any(Verifier.HasValidSignature) ? location : null;

                if (remoteInterface != null) {
                    Event<GetResponseInterface>.RaiseFirst().SignatureValidation(result, !string.IsNullOrEmpty(result), string.IsNullOrEmpty(result) ? null : Verifier.GetPublisherName(result));
                }
                // GS01: if all the locations end up invalid, and we're trying to remove a package 

                return _localValidatedLocation = result;
            }
        }

        private RegistryView _generalPackageSettings;

        internal RegistryView GeneralPackageSettings {
            get {
                return
                    _generalPackageSettings ?? (_generalPackageSettings = PackageManagerSettings.PerPackageSettings[_package.CanonicalName.GeneralName]);
            }
        }

        private RegistryView _packageSettings;

        internal RegistryView PackageSettings {
            get {
                return _packageSettings ?? (_packageSettings = PackageManagerSettings.PerPackageSettings[_package.CanonicalName]);
            }
        }

        private int _lastProgress;
        public int DownloadProgress { get; set; }

        public int DownloadProgressDelta {
            get {
                var p = DownloadProgress;
                var result = p - _lastProgress;
                if (result < 0) {
                    return 0;
                }

                _lastProgress = p;
                return result;
            }
        }
    }
}