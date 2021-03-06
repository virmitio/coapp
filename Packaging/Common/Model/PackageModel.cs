﻿//-----------------------------------------------------------------------
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

namespace CoApp.Packaging.Common.Model {
    using System;
    using System.Xml;
    using System.Xml.Serialization;
    using Toolkit.Collections;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Win32;

    [XmlRoot(ElementName = "Package", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class PackageModel {
        // Elements marked with XmlIgnore won't persist in the package feed as themselves
        // they get persisted as elements in the Atom Format (so that we have a suitable Atom feed to look at)

        public PackageModel() {
            XmlSerializer = new XmlSerializer(GetType());
            PackageDetails = new PackageDetails();
        }

        [XmlIgnore]
        public CanonicalName CanonicalName { get; set; }

        [XmlAttribute("CanonicalName")]
        public string CanonicalNameSurrogate {
            get {
                return CanonicalName.ToString();
            }
            set {
                CanonicalName = CanonicalName.Parse(value);
            }
        }

        [XmlAttribute]
        public string Name {
            get {
                return CanonicalName.Name;
            }
            set {
                // ignore this value when deserializing, check just for consistency.
                if (null != CanonicalName && CanonicalName.Name != value) {
                    throw new CoAppException("PackageModel.Name is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Name));
                }
            }
        }

        [XmlAttribute]
        public string Flavor {
            get {
                return CanonicalName.Flavor;
            }
            set {
                if (null != CanonicalName && CanonicalName.Flavor != value) {
                    throw new CoAppException("PackageModel.Flavor is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Flavor));
                }
            }
        }

        [XmlIgnore]
        public PackageType PackageType {
            get {
                return CanonicalName.PackageType;
            }
        }

        // Workaround to get around stupid .NET limitation of not being able to let a class/struct serialize as an attribute. #FAIL
        [XmlAttribute("PackageType")]
        public string PackageTypeSurrogate {
            get {
                return PackageType;
            }
            set {
                // ignore this value when deserializing, check just for consistency.
                if (null != CanonicalName && CanonicalName.PackageType != value) {
                    throw new CoAppException("PackageModel.PackageType is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.PackageType));
                }
            }
        }

        [XmlIgnore]
        public Architecture Architecture {
            get {
                return CanonicalName.Architecture;
            }
        }

        // Workaround to get around stupid .NET limitation of not being able to let a class/struct serialize as an attribute. #FAIL
        [XmlAttribute("Architecture")]
        public string ArchitectureSurrogate {
            get {
                return Architecture;
            }
            set {
                // ignore this value when deserializing, check just for consistency.
                if (null != CanonicalName && CanonicalName.Architecture != value) {
                    throw new CoAppException("PackageModel.Architecture is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Architecture));
                }
            }
        }

        [XmlIgnore]
        public FourPartVersion Version {
            get {
                return CanonicalName.Version;
            }
        }

        // Workaround to get around stupid .NET limitation of not being able to let a class/struct serialize as an attribute. #FAIL
        [XmlAttribute("Version")]
        public string VersionSurrogate {
            get {
                return Version;
            }
            set {
                if (null != CanonicalName && CanonicalName.Version != value) {
                    throw new CoAppException("PackageModel.Version is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.Version));
                }
            }
        }

        [XmlAttribute]
        public string PublicKeyToken {
            get {
                return CanonicalName.PublicKeyToken;
            }
            set {
                if (null != CanonicalName && CanonicalName.PublicKeyToken != value) {
                    throw new CoAppException("PackageModel.PublicKeyToken is a read-only field, and deserializing is inconsistent (value:'{0}', should be:'{1}')".format(value, CanonicalName.PublicKeyToken));
                }
            }
        }

        [XmlAttribute]
        public string DisplayName { get; set; }

        [XmlAttribute]
        public string Vendor;

        [XmlElement("BindingPolicy", IsNullable = false)]
        public BindingPolicy BindingPolicy { get; set; }

        [XmlAttribute]
        public string RelativeLocation { get; set; }

        [XmlAttribute]
        public string Filename { get; set; }

        [XmlElement(IsNullable = false)]
        public XList<Role> Roles { get; set; }

        [XmlElement("Dependencies", IsNullable = false)]
        public XDictionary<CanonicalName, XList<Uri>> Dependencies { get; set; }

        [XmlElement(IsNullable = false)]
        public XList<Feature> Features { get; set; }

        [XmlElement(IsNullable = false)]
        public XList<Feature> RequiredFeatures { get; set; }

        [XmlElement(IsNullable = false, ElementName = "Details")]
        public PackageDetails PackageDetails { get; set; }
        
        [XmlElement("Feeds")]
        public XList<Uri> Feeds { get; set; }

        [XmlIgnore]
        public string CosmeticName {
            get {
                return "{0}-{1}-{2}".format(Name, Version.ToString(), Architecture);
            }
        }

        [XmlIgnore]
        public XList<Uri> Locations { get; set; }
        
        [XmlIgnore]
        public Composition CompositionData { get; set; }

        [XmlIgnore]
        public XmlSerializer XmlSerializer;

        // soak up anything we don't recognize
        [XmlAnyAttribute, NotPersistable]
        public XmlAttribute[] UnknownAttributes;

        [XmlAnyElement, NotPersistable]
        public XmlElement[] UnknownElements;
    }
}