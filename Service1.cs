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

        private List<string> filepathsToProcess;
        private BackgroundWorker backgroundFileCopier;
        private const uint MAX_RETRIES_PER_FILE = 10;

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
        // Set up the BackgroundWorker object by  
        // attaching event handlers.  
        private void InitializeBackgroundWorker()
        {
            eventLog1.WriteEntry("InitializeBackgroundWorker");
            backgroundFileCopier = new System.ComponentModel.BackgroundWorker();

            backgroundFileCopier.WorkerReportsProgress = true;
            backgroundFileCopier.WorkerSupportsCancellation = true;

            backgroundFileCopier.DoWork +=
                new DoWorkEventHandler(backgroundFileCopier_DoWork);
            backgroundFileCopier.RunWorkerCompleted +=
                new RunWorkerCompletedEventHandler(
            backgroundFileCopier_RunWorkerCompleted);
            backgroundFileCopier.ProgressChanged +=
                new ProgressChangedEventHandler(
            backgroundFileCopier_ProgressChanged);
        }


		// The main entry point for the process
		static void Main()
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
			if( disposing )
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

            // FileCopy related functionality
            filepathsToProcess = new List<string>();

            // Copy existing files before starting
            foreach (string newPath in Directory.GetFiles(path1, "*.*",
                SearchOption.AllDirectories))
                File.Move(newPath, newPath.Replace(path1, path2));

            InitializeBackgroundWorker();
            CreateWatcher();
 
		}
 
		/// <summary>
		/// Stop this service.
		/// </summary>
		protected override void OnStop()
		{
			// TODO: Add code here to perform any tear-down necessary to stop your service.
			eventLog1.WriteEntry("Service stopped");
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
            if (backgroundFileCopier.IsBusy != true)
            {
                // Start the asynchronous operation, if not yet started
                backgroundFileCopier.RunWorkerAsync();
            }
            filepathsToProcess.Add(e.FullPath);
            eventLog1.WriteEntry("Added to queue.");
        }

        // This event handler is where the time-consuming work is done. 
        private void backgroundFileCopier_DoWork(object sender, DoWorkEventArgs e)
        {
            eventLog1.WriteEntry("backgroundFileCopier_DoWork");
            BackgroundWorker worker = sender as BackgroundWorker;

            eventLog1.WriteEntry("filepathsToProcess.Count: " + filepathsToProcess.Count);

            UInt16 retries = 0;
            while( filepathsToProcess.Count > 0)
            {
                // Call ReportProgress to just update UI, if needed
                worker.ReportProgress(0); 

                if (worker.CancellationPending == true)
                {
                    eventLog1.WriteEntry("CancellationPending");
                    e.Cancel = true;
                    foreach (var i in filepathsToProcess)
                    {
                        filepathsToProcess.Remove(i);
                    }
                }
                else
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append(path2 + Path.GetFileName(filepathsToProcess[0]));

                        eventLog1.WriteEntry("filepath1: " + filepathsToProcess[0]);
                        eventLog1.WriteEntry("filepath2: " + sb.ToString());
                        File.Move(filepathsToProcess[0], sb.ToString());
                        // Success
                        eventLog1.WriteEntry("Copied.");
                        filepathsToProcess.RemoveAt(0);
                        eventLog1.WriteEntry("Removed from queue");
                        retries = 0;
                    }
                    catch (Exception ex)
                    {
                        eventLog1.WriteEntry("Exception:" + ex.ToString());

                        // If this file no longer exists then ignore it
                        if (ex.Message.Contains("already exists"))
                        {
                            eventLog1.WriteEntry("Already exists: " + filepathsToProcess[0]);
                            // Delete original even if cannot overwrite
                            File.Delete(filepathsToProcess[0]);
                            filepathsToProcess.RemoveAt(0);
                            retries = 0;
                            eventLog1.WriteEntry("Removed from queue");
                        }

                        else if (ex.Message.Contains("Could not find file"))
                        {
                            eventLog1.WriteEntry("File not found: " + filepathsToProcess[0]);
                            filepathsToProcess.RemoveAt(0);
                            retries = 0;
                            eventLog1.WriteEntry("Removed from queue");
                        }
                        else
                        {
                            eventLog1.WriteEntry("File is busy... Waiting for file to become available.");
                            Thread.Sleep(200);
                            retries++;
                            if (retries > MAX_RETRIES_PER_FILE)
                            {
                                retries = 0;
                                filepathsToProcess.RemoveAt(0);
                                eventLog1.WriteEntry("Max retries reached. Removed from queue");
                            }
                        }
                    }
                }
            }
            eventLog1.WriteEntry("DONE!");
            worker.ReportProgress(100);
        }

        // This event handler updates the progress. 
        private void backgroundFileCopier_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            eventLog1.WriteEntry("backgroundFileCopier_ProgressChanged");
            // update UI or smt here
        }

        // This event handler deals with the results of the background operation. 
        private void backgroundFileCopier_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                eventLog1.WriteEntry("Canceled!");
            }
            else if (e.Error != null)
            {
                eventLog1.WriteEntry("Error: " + e.Error.Message);
            }
            else
            {
                eventLog1.WriteEntry("Completed");
            }
        }
	}
}
