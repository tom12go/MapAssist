/**
 *   Copyright (C) 2021 okaygo
 *   
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using System;
using System.Threading;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;

namespace MapAssist
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                using (IKeyboardMouseEvents globalHook = Hook.GlobalEvents())
                {
                    using (var overlay = new Overlay(globalHook))
                    {
                        overlay.Run();
                    }
                }
            }
            catch (Exception e)
            {
                ProcessException(e);
            }
        }

        private static void ProcessException(Exception e)
        {
            var message = e.Message + Environment.NewLine + Environment.NewLine + e.StackTrace;

            MessageBox.Show(message, "MapAssist Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            Application.Exit();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ProcessException((Exception) e.ExceptionObject);
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ProcessException(e.Exception);
        }
    }
}
