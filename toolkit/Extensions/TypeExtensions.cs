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


namespace CoApp.Toolkit.Extensions {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;

    public static class TypeExtensions {
        private static readonly Dictionary<Type, MethodInfo> TryParsers = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, ConstructorInfo> TryStrings = new Dictionary<Type, ConstructorInfo>();

        private static MethodInfo GetTryParse(Type parsableType) {
            if (!TryParsers.ContainsKey(parsableType)) {
                if (parsableType.IsPrimitive || parsableType.IsValueType || parsableType.GetConstructor(new Type[] { }) != null) {
                    TryParsers.Add(parsableType, parsableType.GetMethod("TryParse", new[] { typeof(string), parsableType.MakeByRefType() }));
                } else {
                    // if they don't have a default constructor, 
                    // it's not going to be 'parsable'
                    TryParsers.Add(parsableType, null);
                }
            }
            return TryParsers[parsableType];
        }

        private static ConstructorInfo GetStringConstructor(Type parsableType) {
            if (!TryStrings.ContainsKey(parsableType)) {
                TryStrings.Add(parsableType, parsableType.GetConstructor(new Type[] {typeof(string) }));
            }
            return TryStrings[parsableType];
        }

        public static bool IsConstructableFromString(this Type stringableType) {
            return GetStringConstructor(stringableType) != null;
        }

        public static bool IsParsable(this Type parsableType) {
            return GetTryParse(parsableType) != null || IsConstructableFromString(parsableType);
        }

        public static object ParseString(this Type parsableType, string value) {
            if( parsableType == typeof(string)) {
                return value;
            }
            var tryParse = GetTryParse(parsableType);

            if (tryParse != null) {
                if (!string.IsNullOrEmpty(value)) {
                    var pz = new[] {value, Activator.CreateInstance(parsableType)};
                    
                    // returns the default value if it's not successful.
                    tryParse.Invoke(null, pz);
                    return pz[1];
                }
                return Activator.CreateInstance(parsableType);
            }

            return value == null ? null : GetStringConstructor(parsableType).Invoke(new object[] {value});
        }

        public static bool IsDictionary(this Type dictionaryType) {
            return typeof (IDictionary).IsAssignableFrom(dictionaryType);
        }

        public static bool IsIEnumerable(this Type ienumerableType) {
            return typeof (IDictionary).IsAssignableFrom(ienumerableType);
        }
    }
}