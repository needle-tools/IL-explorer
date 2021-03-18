﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Disassembler.Editor.Plugins.System.Reflection.ExtensionMethods
{
    public static class MethodSignatureTools
    {
        public static string GetSignature(this MethodInfo method, bool invokable) {

            var signatureBuilder = new StringBuilder();

            // Add our method accessors if it's not invokable
            if (!invokable) {
                signatureBuilder.Append(GetMethodAccessorSignature(method));
                signatureBuilder.Append(" ");
            }

            // Add method name
            signatureBuilder.Append(method.Name);

            // Add method generics
            if (method.IsGenericMethod) {
                signatureBuilder.Append(GetGenericSignature(method));
            }

            // Add method parameters
            signatureBuilder.Append(GetMethodArgumentsSignature(method, invokable));

            return signatureBuilder.ToString();
        }

        public static string GetMethodAccessorSignature(this MethodInfo method) {
            string signature = null;

            if (method.IsAssembly) {
                signature = "internal ";

                if (method.IsFamily)
                    signature += "protected ";
            } else if (method.IsPublic) {
                signature = "public ";
            } else if (method.IsPrivate) {
                signature = "private ";
            } else if (method.IsFamily) {
                signature = "protected ";
            }

            if (method.IsStatic)
                signature += "static ";

            signature += TypeSignatureTools.GetSignature(method.ReturnType);

            return signature;
        }

        public static string GetGenericSignature(this MethodInfo method) {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (!method.IsGenericMethod) throw new ArgumentException($"{method.Name} is not generic.");

            return TypeSignatureTools.BuildGenericSignature(method.GetGenericArguments());
        }

        public static string GetMethodArgumentsSignature(this MethodInfo method, bool invokable) {
            var isExtensionMethod = method.IsDefined(typeof(global::System.Runtime.CompilerServices.ExtensionAttribute), false);
            var methodParameters = method.GetParameters().AsEnumerable();

            // If this signature is designed to be invoked and it's an extension method
            if (isExtensionMethod && invokable) {
                // Skip the first argument
                methodParameters = methodParameters.Skip(1);
            }

            var methodParameterSignatures = methodParameters.Select(param => {
                var signature = string.Empty;

                if (param.ParameterType.IsByRef)
                    signature = "ref ";
                else if (param.IsOut)
                    signature = "out ";
                else if (isExtensionMethod && param.Position == 0)
                    signature = "this ";

                if (!invokable) {
                    signature += TypeSignatureTools.GetSignature(param.ParameterType) + " ";
                }

                signature += param.Name;

                return signature;
            });

            var methodParameterString = "(" + string.Join(", ", methodParameterSignatures) + ")";

            return methodParameterString;
        }
    }
}
