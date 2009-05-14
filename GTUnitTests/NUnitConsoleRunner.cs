//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;

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
