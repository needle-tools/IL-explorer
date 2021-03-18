using UnityEngine;

namespace Disassembler.TestScripts
{
    public class TestMethods
    {
        public class NestedClass {
            public float someTest  = 0f;

            public class ReallyFancy<T, T1, T2, T3> {
                T someInternal;
                public T somePublic = default;
            }
        }
        public Rigidbody rb;
        public ScanTest st;

        private float myPrivateBackingField;
        public float MyFloatProperty
        {
            get
            {
                st = null;
                return myPrivateBackingField;
            }
            set
            {
                myPrivateBackingField = value;
            }
        }
        
        public float GetterWithConstantReturnValue => 42;

        public float InOutMuchLoop(float in1, float in2, ref float ref3, out float out4)
        {
            out4 = 0;
            for (var i = 0; i < 100; i++)
                out4 += in2;
            return out4;
        }

#pragma warning disable 219
        public void ALotOfVariables()
        {
            var testvar1 = 42;
            var testvar2 = "hello world";
            double testvar3 = 4;
            testvar1 *= 2;
            var myVec = new Vector3();
            var mh = false;
            int anotherInt = 123532;
            testvar1 += anotherInt;
        }
#pragma warning restore

        public int Jumping()
        {
            var res = 0;
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    res += 1;
                }
            }

            return res;
        }
    }
}