using System.Text;
using System;
using System.IO;
using System.Diagnostics;

namespace MapAssist.Files
{
    public class FileManager
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();
        private string _fullPath;

        public FileManager(string fileName)
        {
            if (Path.IsPathRooted(fileName))
            {
                _fullPath = fileName;
            } else
            {
                _fullPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            }
        }

        public string GetPath() { return _fullPath; }
        public string GetAbsolutePath() { return _fullPath; }
        public bool FileExists()
        {
            return System.IO.File.Exists(_fullPath);
        }

        public void CreateFile()
        {
            if (FileExists())
            {
                throw new Exception($"Trying to create {_fullPath} even though file exists..");
            }

            try
            {
                System.IO.File.Create(_fullPath).Close();
            }
            catch (Exception e)
            {
                throw new Exception($"Trying to create {_fullPath} : {e.Message}");
            }
        }

        public void DeleteFile()
        {
            try
            {
                if (FileExists())
                {
                    System.IO.File.Delete(_fullPath);
                    _log.Debug($"Removed {_fullPath}");
                }
            }
            catch (Exception e)
            {
                _log.Debug(e, $"Tried to remove {_fullPath} but got error.");
            }
        }

        public string ReadFile()
        {
            var sb = new StringBuilder();
            try
            {
                // Open the file to read from.
                using (StreamReader sr = File.OpenText(GetPath()))
                {
                    string s;
                    while ((s = sr.ReadLine()) != null)
                    {
                        sb.Append($"{s}\n");
                    }
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                _log.Debug(e, $"The file {_fullPath} could not be read.");
            }
            if (sb.ToString().Length == 0)
            {
                _log.Debug($"The file {_fullPath} was empty...");
            }

            return sb.ToString();
        }

        public bool WriteFile(string content)
        {
            try
            {
                using (StreamWriter sw = File.CreateText(_fullPath))
                {
                    sw.WriteLine(content);
                    sw.Close();
                }
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                _log.Debug(e, $"The file {_fullPath} could not be written.");
                return false;
            }
            return true;
        }
    }
}
