using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TestGameAssets
{
    class Program
    {
        static void Main(string[] args)
        {
            Packer.Pack("..\\..\\_ATLAS", "..\\..\\..\\TestGame\\Content"); //THIS TOTALLY SUCKS :(
        }
    }
}
