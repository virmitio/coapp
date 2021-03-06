﻿// 
//  Copyright 2011 Ekon Benefits
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

namespace CoApp.Toolkit.ImpromptuInterface.Dynamic {
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Microsoft.CSharp.RuntimeBinder;
    using Optimization;

    /// <summary>
    ///   Get access to target of original proxy
    /// </summary>
    public interface IForwarder {
        /// <summary>
        ///   Gets the target.
        /// </summary>
        /// <value> The target. </value>
        object Target { get; }
    }

    /// <summary>
    ///   Proxies Calls allows subclasser to override do extra actions before or after base invocation
    /// </summary>
    /// <remarks>
    ///   This may not be as efficient as other proxies that can work on just static objects or just dynamic objects... Consider this when using.
    /// </remarks>
    public abstract class ImpromptuForwarder : ImpromptuObject, IForwarder {
        /// <summary>
        ///   Initializes a new instance of the <see cref="ImpromptuForwarder" /> class.
        /// </summary>
        /// <param name="target"> The target. </param>
        protected ImpromptuForwarder(object target) {
            Target = target;
        }

#if !SILVERLIGHT
        /// <summary>
        ///   Initializes a new instance of the <see cref="ImpromptuForwarder" /> class.
        /// </summary>
        /// <param name="info"> The info. </param>
        /// <param name="context"> The context. </param>
        protected ImpromptuForwarder(SerializationInfo info,
            StreamingContext context) : base(info, context) {
            Target = info.GetValue<IDictionary<string, object>>("Target");
        }

        /// <summary>
        ///   Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info"> The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> to populate with data. </param>
        /// <param name="context"> The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext" /> ) for this serialization. </param>
        /// <exception cref="T:System.Security.SecurityException">The caller does not have the required permission.</exception>
        public override void GetObjectData(SerializationInfo info, StreamingContext context) {
            base.GetObjectData(info, context);
            info.AddValue("Target", Target);
        }
#endif

        /// <summary>
        ///   Returns the enumeration of all dynamic member names.
        /// </summary>
        /// <returns> A sequence that contains dynamic member names. </returns>
        public override IEnumerable<string> GetDynamicMemberNames() {
            if (!KnownInterfaces.Any()) {
                var tDyanmic = Impromptu.GetMemberNames(CallTarget, dynamicOnly: true);
                if (!tDyanmic.Any()) {
                    return Impromptu.GetMemberNames(CallTarget);
                }
            }
            return base.GetDynamicMemberNames();
        }

        /// <summary>
        ///   Gets or sets the target.
        /// </summary>
        /// <value> The target. </value>
        protected object Target { get; set; }

        object IForwarder.Target {
            get {
                return Target;
            }
        }

        /// <summary>
        ///   Gets the call target.
        /// </summary>
        /// <value> The call target. </value>
        protected virtual object CallTarget {
            get {
                return Target;
            }
        }

        /// <summary>
        ///   Provides the implementation for operations that get member values. Classes derived from the <see
        ///    cref="T:System.Dynamic.DynamicObject" /> class can override this method to specify dynamic behavior for operations such as getting a value for a property.
        /// </summary>
        /// <param name="binder"> Provides information about the object that called the dynamic operation. The binder.Name property provides the name of the member on which the dynamic operation is performed. For example, for the Console.WriteLine(sampleObject.SampleProperty) statement, where sampleObject is an instance of the class derived from the <see
        ///    cref="T:System.Dynamic.DynamicObject" /> class, binder.Name returns "SampleProperty". The binder.IgnoreCase property specifies whether the member name is case-sensitive. </param>
        /// <param name="result"> The result of the get operation. For example, if the method is called for a property, you can assign the property value to <paramref
        ///    name="result" /> . </param>
        /// <returns> true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a run-time exception is thrown.) </returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            if (CallTarget == null) {
                result = null;
                return false;
            }

            if (Impromptu.InvokeIsEvent(CallTarget, binder.Name)) {
                result = new ImpromptuForwarderAddRemove();
                return true;
            }

            result = Impromptu.InvokeGet(CallTarget, binder.Name);

            return true;
        }

        /// <summary>
        ///   Provides the implementation for operations that invoke an object. Classes derived from the <see
        ///    cref="T:System.Dynamic.DynamicObject" /> class can override this method to specify dynamic behavior for operations such as invoking an object or a delegate.
        /// </summary>
        /// <param name="binder"> Provides information about the invoke operation. </param>
        /// <param name="args"> The arguments that are passed to the object during the invoke operation. For example, for the sampleObject(100) operation, where sampleObject is derived from the <see
        ///    cref="T:System.Dynamic.DynamicObject" /> class, <paramref name="args[0]" /> is equal to 100. </param>
        /// <param name="result"> The result of the object invocation. </param>
        /// <returns> true if the operation is successful; otherwise, false. If this method returns false, the run-time binder of the language determines the behavior. (In most cases, a language-specific run-time exception is thrown. </returns>
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
            if (CallTarget == null) {
                result = null;
                return false;
            }

            var tArgs = Util.NameArgsIfNecessary(binder.CallInfo, args);

            try {
                result = Impromptu.Invoke(CallTarget, tArgs);
            } catch (RuntimeBinderException) {
                result = null;
                try {
                    Impromptu.InvokeAction(CallTarget, tArgs);
                } catch (RuntimeBinderException) {
                    return false;
                }
            }
            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            if (CallTarget == null) {
                result = null;
                return false;
            }

            object[] tArgs = Util.NameArgsIfNecessary(binder.CallInfo, args);

            try {
                result = Impromptu.InvokeMember(CallTarget, binder.Name, tArgs);
            } catch (RuntimeBinderException) {
                result = null;
                try {
                    Impromptu.InvokeMemberAction(CallTarget, binder.Name, tArgs);
                } catch (RuntimeBinderException) {
                    return false;
                }
            }
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value) {
            if (CallTarget == null) {
                return false;
            }

            if (Impromptu.InvokeIsEvent(CallTarget, binder.Name) && value is ImpromptuForwarderAddRemove) {
                var tValue = value as ImpromptuForwarderAddRemove;

                if (tValue.IsAdding) {
                    Impromptu.InvokeAddAssign(CallTarget, binder.Name, tValue.Delegate);
                } else {
                    Impromptu.InvokeSubtractAssign(CallTarget, binder.Name, tValue.Delegate);
                }

                return true;
            }

            Impromptu.InvokeSet(CallTarget, binder.Name, value);

            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
            if (CallTarget == null) {
                result = null;
                return false;
            }

            object[] tArgs = Util.NameArgsIfNecessary(binder.CallInfo, indexes);

            result = Impromptu.InvokeGetIndex(CallTarget, tArgs);
            return true;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value) {
            if (CallTarget == null) {
                return false;
            }

            var tCombinedArgs = indexes.Concat(new[] {value}).ToArray();
            object[] tArgs = Util.NameArgsIfNecessary(binder.CallInfo, tCombinedArgs);

            Impromptu.InvokeSetIndex(CallTarget, tArgs);
            return true;
        }

        /// <summary>
        ///   Equals the specified other.
        /// </summary>
        /// <param name="other"> The other. </param>
        /// <returns> </returns>
        public bool Equals(ImpromptuForwarder other) {
            if (ReferenceEquals(null, other)) {
                return ReferenceEquals(null, CallTarget);
            }
            if (ReferenceEquals(this, other)) {
                return true;
            }
            return Equals(other.CallTarget, CallTarget);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return ReferenceEquals(null, CallTarget);
            }
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (obj.GetType() != typeof (ImpromptuForwarder)) {
                return false;
            }
            return Equals((ImpromptuForwarder)obj);
        }

        public override int GetHashCode() {
            return (CallTarget != null ? CallTarget.GetHashCode() : 0);
        }
    }
}