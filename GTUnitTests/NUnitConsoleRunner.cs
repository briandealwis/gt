using System;
using System.Collections.Generic;
using System.Text;

namespace GT.UnitTests.Runner
{
    // Idea from <http://stewartr.blogspot.com/2006/09/debugging-nunit-in-visual-studio.html>
    class NUnitConsoleRunner
    {
        [STAThread]
        static void Main(string[] args)
        {
            string[] newArgs = new string[] { "/nologo", "/wait", "..\\..\\GTUnitTests.csproj" };
            if (args.Length > 0)
            {
                string[] builtArgs = new string[newArgs.Length + args.Length];
                args.CopyTo(builtArgs, 0);
                newArgs.CopyTo(builtArgs, args.Length);
                args = builtArgs;
            }
            else
            {
                args = newArgs;
            }
            NUnit.ConsoleRunner.Runner.Main(args);
            // NUnit.Gui.AppEntry.Main(new string[] { "/noload", "/run", "/console", "GTUnitTests.exe" });
        }
    }
}
