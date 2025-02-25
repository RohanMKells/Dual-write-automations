// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using CommandLine;
using DWLibary;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DWHelperUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    

    public partial class MainWindow : Window
    {
        private Process process;/// 
        ConcurrentQueue<string> outputQueue;//= new ConcurrentQueue<string>();

        public MainWindow()
        {
            InitializeComponent();
            initConfigFiles();
            initEnums();
            initFormSettings();
            outputQueue = new ConcurrentQueue<string>();
        }

        private void initFormSettings()
        {
            StopProcess.IsEnabled = false;

        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {

            if (!isValidStart())
            {
               // outputLog.AppendText("")
                return;
            }
            outputQueue = new ConcurrentQueue<string>();

            process = new Process();
            process.StartInfo.FileName = "DWHelperCMD.exe";
            process.StartInfo.Arguments = string.Join(" ", getArgsList());
            process.EnableRaisingEvents= true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            //process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            //process.OutputDataReceived += (sendingProcess, outLine) =>
            //{
            //    if (!String.IsNullOrEmpty(outLine.Data))
            //    {
            //        outputQueue.Enqueue(outLine.Data);
            //    }
            //};
            process.OutputDataReceived += (sendingProcess, outLine) =>
            {
                if (!String.IsNullOrEmpty(outLine.Data))

                {
                    var data = outLine.Data.ToString();
                    data = data.Replace("info: DWHelper.DWHostedService[0]", "");
                    data = data.Replace("info: Microsoft.Hosting.Lifetime[0]", "");
                    data = data.Replace("fail: DWHelper.DWHostedService[0]", "ERROR");


                    outputQueue.Enqueue(data);
                }
            };

            process.Exited += Process_Exited;
      
            
            process.Start();
            process.BeginOutputReadLine();

            runThreadProcessLog();


            StartProcess.IsEnabled = false;
            StopProcess.IsEnabled = true;

        }

        private void runThreadProcessLog()
        {
            new Thread(() =>
            {
                Thread.Sleep(1000);
                while (!process.HasExited || !outputQueue.IsEmpty)
                {
                    while (outputQueue.TryDequeue(out string line))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (line != null && line.Length > 0)
                            {
                                outputLog.AppendText($"{Environment.NewLine}{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {line}");
                                outputLog.ScrollToEnd();
                            }
                        });
                        Console.WriteLine(line);
                        // Do something with the output, e.g. store it in a variable or list
                    }
                    Thread.Sleep(500);
                }

                string smth = "Thread ended";

            }).Start();
        }


        private void Process_Exited(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StartProcess.IsEnabled = true;
                StopProcess.IsEnabled = false;
                initConfigFiles();
            });
            
           // throw new NotImplementedException();
        }

        private bool isValidStart()
        {
            bool ret = true;

            validateUri();

            if (envURL.Text == String.Empty)
            {
                outputLog.AppendText(Environment.NewLine + "URL is empty and needs to be specified!");
                outputLog.ScrollToEnd();
                ret = false;
            }

            return ret;
        }

        private List<string> getArgsList()
        {
            List<string> ret = new List<string>();

            ret.Add("-u");
            ret.Add($"\"{username.Text}\"");
            ret.Add("-p");
            ret.Add($"\"{password.Password}\"");

            ret.Add("-e");
            ret.Add($"\"{envURL.Text}\"");

            DWEnums.RunMode localMode = (DWEnums.RunMode)runMode.SelectedValue;

            if (localMode != null && localMode != DWEnums.RunMode.none)
            {
                ret.Add("--runmode");
                ret.Add($"\"{localMode.ToString()}\"");

                if(localMode == DWEnums.RunMode.export)
                {
                    ret.Add("-s");
                    ret.Add($"\"{exportStatus.Text}\"");
                }

            }

            if (applySolutions.IsChecked == false)
            {
                ret.Add("--nosolutions");
            }

            if (adowikiupload.IsChecked == true)
            {
                ret.Add("--useadowikiupload");
            }
            string configName = customConfigFile.SelectedItem == null ? "" : customConfigFile.SelectedItem.ToString();

            if (configName != null && configName != String.Empty)
            {
                if(configName != "DWHelper.dll.config")
                {
                    ret.Add("-c");
                    ret.Add(($"\"{configName}\""));
                }
            }

            if(logLevel.SelectedItem != null)
            {
                ret.Add("-l");
                ret.Add(($"\"{logLevel.SelectedValue}\""));
            }


            return ret; 
        }


        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                Dispatcher.Invoke(() =>
                {
                    var data = outLine.Data.ToString(); 
                    data = data.Replace("info: DWHelper.DWHostedService[0]", "");
                    data = data.Replace("info: Microsoft.Hosting.Lifetime[0]", "");
                    data = data.Replace("info: Microsoft.Hosting.Lifetime[0]", "ERROR!");

                    if (data != null && data.Length > 0)
                    {
                        outputLog.AppendText($"{Environment.NewLine}{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: {data}");
                        outputLog.ScrollToEnd();
                    }
                });
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {

            Properties.Settings.Default.runmode = runMode.SelectedValue.ToString();
            Properties.Settings.Default.Save();
            base.OnClosing(e);
        }

        private void validateUri()
        {
            string ret = String.Empty;
           
            try
            {
                if (envURL.Text == String.Empty)
                    return;

                UriBuilder builder = new UriBuilder(envURL.Text);
                if (!builder.Uri.AbsoluteUri.ToUpper().Contains("DYNAMICS.COM"))
                {
                    MessageBox.Show("URL is not valid");
                    envURL.Text = String.Empty;
                    return;
                }

                ret = builder.Uri.Host.Replace("www.", "");

                envURL.Text = ret;
            }
            catch
            {
                MessageBox.Show("URL is not valid");
                envURL.Text = String.Empty;
            }

            return;
        }

        private void addEnvironment_Click(object sender, RoutedEventArgs e)
        {

            validateUri();
            if (envURL.Text == String.Empty)
                return;

            StringCollection collection = new StringCollection();
            collection.AddRange(envList.Items.Cast<string>().ToArray());

            if (!collection.Contains(envURL.Text))
            {
                collection.Add(envURL.Text);

                Properties.Settings.Default.envList = collection;
                envList.Items.Refresh();
            }
        }

        private void envList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(envList.SelectedItem== null) return;

            string selectedItem = envList.SelectedItem.ToString();

            envURL.Text = selectedItem;
        }

        private void initConfigFiles()
        {

            string[] configFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.config");
            List<string> configList = new List<string>();
            foreach (string configFile in configFiles)
            {
               FileInfo info = new FileInfo(configFile);

                if (info.Name == "DWHelperUI.dll.config")
                    continue;

               configList.Add(info.Name);
            }

            customConfigFile.ItemsSource = configList;
           

        }


        private void removeList_Click(object sender, RoutedEventArgs e)
        {
            validateUri();

            StringCollection collection = new StringCollection();
            collection.AddRange(envList.Items.Cast<string>().ToArray());
            collection.Remove(envURL.Text);
            Properties.Settings.Default.envList = collection;
            envList.Items.Refresh();

        }

        private void envURL_LostFocus(object sender, RoutedEventArgs e)
        {
            validateUri();
            setHyperlinkURL();
        }

        private void StopProcess_Click(object sender, RoutedEventArgs e)
        {
            if(process!= null) 
                process.Kill();

            foreach(Process p in Process.GetProcessesByName("msedgedriver"))
            {
                p.Kill();
            }

            StopProcess.IsEnabled = false;
            StartProcess.IsEnabled= true;

        }
        private void initEnums()
        {
            runMode.Items.Clear();
            runMode.ItemsSource = System.Enum.GetValues(typeof(DWLibary.DWEnums.RunMode));

            runMode.ItemsSource = Enum.GetValues(typeof(DWEnums.RunMode))
            .Cast<DWEnums.RunMode>()
            .Select(rm => new
            {
                Value = rm,
                Description = DWEnums.DescriptionAttr<DWEnums.RunMode>(rm)
            });

            // var selectedItem = (KeyValuePair<string, string>)Properties.Settings.Default.runmode;
            //runMode.SelectedItem
            DWEnums.RunMode selected; 
             var selectEnum = Enum.TryParse<DWEnums.RunMode>(Properties.Settings.Default.runmode,true, out selected);
            foreach (dynamic item in runMode.Items)
            {
                if (item.Value == selected)
                    runMode.SelectedItem = item;
            }
            //runMode.SelectedItem = runMode.Items.ca
            // runMode.SelectedItem = DWEnums.GetValueFromDescription<DWEnums.RunMode>(Properties.Settings.Default.runmode);



        }

        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DWEnums.RunMode selectedRunMode = (DWEnums.RunMode)runMode.SelectedValue;

            switch(selectedRunMode)
            {
                case DWEnums.RunMode.export:
                    exportSettings.Visibility = Visibility.Visible;
                    applySolutions.IsEnabled = false;
                    adowikiupload.IsEnabled = false;
                    break;


                default:
                    applySolutions.IsEnabled = true;
                    adowikiupload.IsEnabled = true;
                    exportSettings.Visibility = Visibility.Collapsed;
                    break;
            }

            if(selectedRunMode != DWEnums.RunMode.export)
            {
            }
            else
            {
                exportSettings.Visibility = Visibility.Visible;
            }



        }

        
        private void editConfigFile_Click(object sender, RoutedEventArgs e)
        {

            EditConfigForm configForm = new EditConfigForm(customConfigFile.SelectedItem.ToString());
            configForm.ShowDialog();

            setHyperlinkURL();

        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void showLogs_Click(object sender, RoutedEventArgs e)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string logDirectory = System.IO.Path.Combine(currentDirectory, "Logs");

            if (Directory.Exists(logDirectory))
            {
                Process.Start("explorer.exe", logDirectory);
            }
            else
            {
                MessageBox.Show("Logs folder does not exist in the current directory");
            }
        }

        private void setHyperlinkURL()
        {
            try
            {
                if(customConfigFile.SelectedItem != null)
                    GlobalVar.configFileName = customConfigFile.SelectedItem.ToString();

                GlobalVar.initConfig();
                GlobalVar.setdataintegratorURL();

                UriBuilder builder = new UriBuilder(GlobalVar.dataintegratorURL);
                builder.Path = $"dualWrite";
                builder.Query = $"axenv={envURL.Text}";

                dataintegratorURL.NavigateUri = builder.Uri;

                dataintegratorURL.Inlines.Clear();
                dataintegratorURL.Inlines.Add(builder.Uri.AbsoluteUri);

            }
            catch
            {

            }
        }

        private void dataintegratorURL_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            string url = e.Uri.ToString();
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }

        private void customConfigFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setHyperlinkURL();
        }

        private void showLastLog_Click(object sender, RoutedEventArgs e)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string logDirectory = System.IO.Path.Combine(currentDirectory, "Logs");
            var file = Directory.GetFiles(logDirectory, "LOG-*").OrderByDescending(f => File.GetCreationTime(f)).First();
            
            if(file != null)
            {
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            }
        }
    }
}
