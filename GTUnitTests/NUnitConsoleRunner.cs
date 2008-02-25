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
            NUnit.ConsoleRunner.Runner.Main(new string[] { "/nologo", "/wait", "..\\..\\GTUnitTests.csproj" });
            // NUnit.Gui.AppEntry.Main(new string[] { "/noload", "/run", "/console", "GTUnitTests.exe" });
        }
    }
}
