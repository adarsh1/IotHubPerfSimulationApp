﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Agent;
using IotHubServer;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Devices.Client;
using SimulatedDevice;

namespace PerfSimulationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TabItem current;
        TelemetryClient telemetryClient;
        public MainWindow()
        {
            InitializeComponent();
            current = SenderTabItem;
            telemetryClient = new TelemetryClient();
            telemetryClient.Context.User.Id = Environment.UserName;
            telemetryClient.Context.Session.Id = Guid.NewGuid().ToString();
            telemetryClient.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
            SenderTabItem.Content = new SenderView(telemetryClient);
            ReceiverTabItem.Content = new ReceiverView(telemetryClient);
            int maxWorker, maxIOC;
            ThreadPool.GetMaxThreads(out maxWorker, out maxIOC);
            // Change the minimum number of worker threads to four, but
            // keep the old setting for minimum asynchronous I/O 
            // completion threads.
            if (ThreadPool.SetMaxThreads(200, 200))
            {
                // The minimum number of threads was set successfully.
            }
            else
            {
                // The minimum number of threads was not changed.
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var control = sender as TabControl;
            if ((TabItem)control.SelectedItem == current)
                return;
            (current.Content as IDisposable)?.Dispose();
            if (current == SenderTabItem)
            {
                SenderTabItem.Content = new SenderView(telemetryClient);
            }
            else
            {
                ReceiverTabItem.Content = new ReceiverView(telemetryClient);
            }
            current = (TabItem)control.SelectedItem;
        }
    }
}
