using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FileCopyService
{
	public class Service1 : System.ServiceProcess.ServiceBase
	{
		private System.Diagnostics.EventLog eventLog1;
        private const string path1 = @"C:\temp_1\";
        private const string path2 = @"C:\temp_2\";

        //private BackgroundWorker backgroundFileCopier;
        private const uint MAX_WAIT_TIME = 60000;
        List<Thread> threads = new List<Thread>();

		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

        public Service1()
		{
			// This call is required by the Windows.Forms Component Designer.
			InitializeComponent();

            if (!System.Diagnostics.EventLog.SourceExists("FileCopyLogSource"))
			{
                System.Diagnostics.EventLog.CreateEventSource("FileCopyLogSource", "FileCopyLog");
			}

            eventLog1.Source = "FileCopyLogSource";
            eventLog1.Log = "FileCopyLog";			
		}

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.eventLog1 = new System.Diagnostics.EventLog();
			((System.ComponentModel.ISupportInitialize)(this.eventLog1)).BeginInit();
			// 
			// eventLog1
			// 
            this.eventLog1.Log = "FileCopyLog";
            this.eventLog1.Source = "FileCopyLogSource";
			// 
			// Service1
			// 
            this.ServiceName = "FileCopyService";
			((System.ComponentModel.ISupportInitialize)(this.eventLog1)).EndInit();

		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if (disposing)
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		/// <summary>
		/// Set things in motion so your service can do its work.
		/// </summary>
		protected override void OnStart(string[] args)
		{
            eventLog1.WriteEntry("FileCopyService started");

            // Copy existing files before starting
            foreach (string newPath in Directory.GetFiles(path1, "*.*",
                SearchOption.AllDirectories))
                File.Move(newPath, newPath.Replace(path1, path2));

            CreateWatcher();
		}
 
		/// <summary>
		/// Stop this service.
		/// </summary>
		protected override void OnStop()
		{
			// TODO: Add code here to perform any tear-down necessary to stop your service.
			eventLog1.WriteEntry("Service stopped");

            // Stop all threads
            foreach (Thread t in threads) {
                if (t.IsAlive) {
                    eventLog1.WriteEntry("Aborting active thread");
                    t.Abort();
                }
            }
		}
		protected override void OnContinue()
		{
            eventLog1.WriteEntry("FileCopyService continuing");
		}
        

        public void CreateWatcher()
        {
            eventLog1.WriteEntry("CreateWatcher..");
            //Create a new FileSystemWatcher.
            FileSystemWatcher watcher = new FileSystemWatcher();

            //Set the filter
            watcher.Filter = "*.*";

            //Subscribe to the Created event.
            watcher.Created += new
                FileSystemEventHandler(watcher_FileCreated);

            //Set the path to C:\Temp\
            watcher.Path = path1;

            //Enable the FileSystemWatcher events.
            watcher.EnableRaisingEvents = true;
            eventLog1.WriteEntry("..created");
        }

        void watcher_FileCreated(object sender, FileSystemEventArgs e)
        {
            eventLog1.WriteEntry("A new file detected!: " + e.FullPath);
            // Create thread for the new operation
            CreateNewCopierThread(e.FullPath);

            eventLog1.WriteEntry("New thread started");
        }

        // Create new thread object  
        private void CreateNewCopierThread(string filepath)
        {
            Thread t = new Thread(ThreadFunction);
            threads.Add(t);
            t.Start(filepath);
        }

        // This is called once for each thread
        public void ThreadFunction(object data)
        {
            string filepath = data.ToString();
            eventLog1.WriteEntry("ThreadFunction");

            CopyFile(filepath, 0);
            eventLog1.WriteEntry("ThreadFunction exit");
        }

        // Actual copying function, called from ThreadFunction
        private void CopyFile(string filepath, int waitTime)
        {
            eventLog1.WriteEntry("CopyFile, path:" + filepath +
                                 ", waitTime:" + waitTime);
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(path2 + Path.GetFileName(filepath));

                eventLog1.WriteEntry("filepath1: " + filepath);
                eventLog1.WriteEntry("filepath2: " + sb.ToString());
                File.Move(filepath, sb.ToString());
                // Success
                eventLog1.WriteEntry("Copied.");
            }
            catch (Exception ex)
            {
                eventLog1.WriteEntry("Exception:" + ex.ToString());

                int hr = System.Runtime.InteropServices.Marshal.GetHRForException(ex);

                eventLog1.WriteEntry("Err#:" + hr.ToString());

                // If this file no longer exists then ignore it
                if (((int)hr & 0xFFFFFFFF) == 0xC00D001B)
                {
                    eventLog1.WriteEntry("Already exists: " + filepath);
                    // Delete original even if cannot overwrite
                    File.Delete(filepath);
                }
                else if (((int)hr & 0xFFFFFFFF) == 0x80070020)  // -2147024864
                {
                    eventLog1.WriteEntry("Cannot access file.. Waiting for file to become available.");
                    Thread.Sleep(200);
                    if (waitTime < MAX_WAIT_TIME)
                    {
                        CopyFile(filepath, waitTime + 200);
                    }
                    else
                    {
                        eventLog1.WriteEntry("Timeout reached.");
                    }
                }
                else
                {
                    eventLog1.WriteEntry("Unhandled: " + filepath);
                }
            }
        }

        // The main entry point for the process
        public static void Main()
        {
            System.ServiceProcess.ServiceBase[] ServicesToRun;

            // More than one user Service may run within the same process. To add
            // another service to this process, change the following line to
            // create a second service object. For example,
            //
            //   ServicesToRun = New System.ServiceProcess.ServiceBase[] {new Service1(), new MySecondUserService()};
            //
            ServicesToRun = new System.ServiceProcess.ServiceBase[] { new FileCopyService.Service1() };

            System.ServiceProcess.ServiceBase.Run(ServicesToRun);
        }
    }
}
