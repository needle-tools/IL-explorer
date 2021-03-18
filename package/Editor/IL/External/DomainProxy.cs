using System;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using UnityEngine;

namespace Disassembler.Editor.IL.External
{
    public class DomainProxy : MarshalByRefObject
    {
        public DomainProxy(string name, string assemblyPath)
        {
            var dir = Path.GetDirectoryName(assemblyPath);
            Evidence evidence = new Evidence(AppDomain.CurrentDomain.Evidence);
            AppDomainSetup setup = AppDomain.CurrentDomain.SetupInformation;
            var domain = AppDomain.CreateDomain(name, evidence, setup);
            domain.ReflectionOnlyAssemblyResolve += OnResolve;
            var assemblies = domain.GetAssemblies();
            foreach (var a in assemblies)
                Debug.Log(a.FullName);

            var proxyType = typeof(ProxyDomain);
            var asmLoaderProxy = (ProxyDomain) domain.CreateInstanceFrom(proxyType.Assembly.Location, proxyType.FullName).Unwrap();
            asmLoaderProxy.GetAssembly(assemblyPath);
        }

        private Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            Debug.Log("RESOLVE " + args.Name);
            //This handler is called only when the common language runtime tries to bind to the assembly and fails.

            //Retrieve the list of referenced assemblies in an array of AssemblyName.
            var strTempAssmbPath = "";

            var objExecutingAssembly = Assembly.GetExecutingAssembly();
            var arrReferencedAssmbNames = objExecutingAssembly.GetReferencedAssemblies();

            //Loop through the array of referenced assembly names.
            foreach (var strAssmbName in arrReferencedAssmbNames)
            {
                //Check for the assembly names that have raised the "AssemblyResolve" event.
                if (strAssmbName.FullName.Substring(0, strAssmbName.FullName.IndexOf(",", StringComparison.Ordinal)) !=
                    args.Name.Substring(0, args.Name.IndexOf(",", StringComparison.Ordinal))) continue;
                //Build the path of the assembly from where it has to be loaded

                Debug.Log(strAssmbName);
                strTempAssmbPath = "C:\\Myassemblies\\" + args.Name.Substring(0, args.Name.IndexOf(",", StringComparison.Ordinal)) + ".dll";
                break;
            }

            //Load the assembly from the specified path.                    
            var myAssembly = Assembly.LoadFrom(strTempAssmbPath);

            //Return the loaded assembly.
            return myAssembly;
        }
    }

    public class ProxyDomain : MarshalByRefObject
    {
        public void GetAssembly(string AssemblyPath)
        {
            var assembly = Assembly.Load(AssemblyPath);
            Debug.Log("LOADED: " + assembly.FullName);
        }
    }
}