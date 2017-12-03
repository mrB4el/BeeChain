using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BeeCoin
{
    public class FileSystem : Additional
    {
        public struct Config
        {
            public string root_path;
            public string config_path;
            public string temp_path;
            public string db_path;
            public string db_temp_path;
            public string db_blocks_path;
            public int version;

        }

        public Config FSConfig = new Config();

        public int Initialize()
        {
            FSConfig.root_path = @"C:\BeeCoin";
            FSConfig.config_path = FSConfig.root_path + @"\config";
            FSConfig.temp_path = FSConfig.root_path + @"\temp";
            FSConfig.db_path = FSConfig.root_path + @"\database";
            FSConfig.db_temp_path = FSConfig.db_path + @"\tmp";
            FSConfig.db_blocks_path = FSConfig.db_path + @"\blocks";
            FSConfig.version = 3;

            //window.WriteLine("class FileSystem: initialized");
            return 1;
        }

        /// <summary>
        /// Создает корневой каталог программы
        /// </summary>
        /// <param name="root_path">Коревой каталог</param>
        /// <returns>Статус создания,
        /// 0 - без изменений,
        /// 1 - успешное создание,
        /// </returns>
        public async Task<bool> CreateFSAsync(bool clearly = false)
        {
            bool status = false;
            try
            {
                if (clearly)
                {
                    RemoveDirectory(FSConfig.temp_path);
                    RemoveDirectory(FSConfig.db_path);
                    RemoveDirectory(FSConfig.config_path);
                }

                foreach (FieldInfo fi in FSConfig.GetType().GetFields())
                {
                    if (fi.Name != "version")
                    {
                        CreateDirectory(Convert.ToString(fi.GetValue(FSConfig)));
                    }
                }
                if (clearly)
                {
                    byte[] data = new byte[0];
                    string path = FSConfig.config_path + @"\hosts";
                    await AddInfoToFileAsync(path, data, true);
                }

                status = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            return status;
        }

        public async Task CheckFiles()
        {
            byte[] data = Encoding.UTF8.GetBytes("192.168.1.56");
            string path = FSConfig.config_path + @"\hosts";
            await AddInfoToFileAsync(path, data, true);
        }

        /// <summary>
        /// Удаление директории с содержимым
        /// </summary>
        /// <param name="path">путь</param>
        public void RemoveDirectory(string path)
        {
            try
            {
                // Determine whether the directory exists.
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    //Debug.WriteLine("The directory " + path + " was deleted successfully.");
                }
                else
                {
                    //Debug.WriteLine("The directory " + path + " doesnt exist");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("The process failed: {0}", e.ToString());
            }
            finally { }
        }

        /// <summary>
        /// Создание директории
        /// </summary>
        /// <param name="path">путь</param>
        public void CreateDirectory(string path)
        {
            try
            {
                // Determine whether the directory exists.
                if (Directory.Exists(path))
                {
                    //Debug.WriteLine("That path " + path + " exists already.");
                    return;
                }
                else
                {
                    // Try to create the directory.
                    DirectoryInfo di = Directory.CreateDirectory(path);
                    //Debug.WriteLine("The directory " + path + " was created successfully at " + Directory.GetCreationTime(path));
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("The process failed: {0}", e.ToString());
            }
            finally { }
        }

        public async Task AddToFileAsync(string name, string path, byte[] data)
        {
            try
            {
                string full_path = FSConfig.root_path + @"\" + path + @"\" + name;

                FileStream fs = new FileStream(full_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, data.Length, true);

                await fs.WriteAsync(data, 0, data.Length);
                fs.Flush();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        
        /// <summary>
        /// Создание файла, если он не существует
        /// </summary>
        /// <param name="name">Имя</param>
        /// <param name="path">Относительный путь</param>
        /// <param name="data">Содержимое</param>
        /// <returns>
        /// 1 - файл создан и записан,
        /// 2 - файл создан и дописан,
        /// 3 - исключение,
        /// 4 - ошибка в запросе
        /// </returns>
        public async Task<int> AddInfoToFileAsync(string path, byte[] data, bool rewrite = false)
        {
            int status = 0;
            bool trigger = false;
           
            try
            {
                if ((data.Length == 0) || (path.Length == 0))
                {
                    trigger = true;
                    status = 4;
                }
                if (!trigger)
                {

                    string full_path = path;
                    //Console.WriteLine("Requesting: " + full_path);

                    FileStream filestream;

                    int offset = 0;

                    if (rewrite)
                    {
                        filestream = new FileStream(full_path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, true);
                        status = 1;
                    }
                    else
                    {
                        filestream = new FileStream(full_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true);
                        status = 2;
                    }
                    await filestream.WriteAsync(data, offset, data.Length);
                    await filestream.FlushAsync();

                    filestream.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                status = 3;
            }
            return status;
        }

        public async Task<byte[]> GetFromFileAsync(string path)
        {
            byte[] result = new byte[0];
            try
            {
                if (File.Exists(path))
                {
                    
                    FileInfo file = new FileInfo(path);

                    FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, (int)file.Length, true);

                    result = new byte[fs.Length];

                    await fs.ReadAsync(result, 0, (int)fs.Length);
                    fs.Close();
                }
                else
                {
                    Console.WriteLine("File: " + path + " - doesnt exist");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return result;
        }

        public string ReadAllTextFromFileAsync(string path)
        {
            string result = string.Empty;
            try
            {
                if (File.Exists(path))
                {
                    result = File.ReadAllText(path);
                }
                else
                {
                    Console.WriteLine("File: " + path + " - doesnt exist");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return result;
        }
        public List<string> GetFilesListFromDirectory(string path)
        {
            List<string> result = new List<string>(0);

            string[] file = Directory.GetFiles(path);
            
            //Console.WriteLine("There are (" + path + "): ");
            
            for(int i=0; i < file.Length; i++)
            {
                file[i] = Path.GetFileName(file[i]);
            }
            result = file.ToList();

            return result;
        }

        public void MoveFileOnDirectory(string old_path, string new_path)
        {

        }

        public void RemoveFile()
        {

        }
    }
}
