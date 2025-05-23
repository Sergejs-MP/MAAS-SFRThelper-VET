using MAAS_SFRThelper.Services;
using MAAS_SFRThelper.ViewModels;
using MAAS_SFRThelper.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;

using System.Windows.Media.Imaging;

namespace VMS.TPS
{
    public class Script
    {
        // Define the project information for EULA verification
        private const string PROJECT_NAME = "SFRThelper";
        private const string PROJECT_VERSION = "1.0.0";
        private const string LICENSE_URL = "https://varian-medicalaffairsappliedsolutions.github.io/MAAS-SFRThelper/";
        private const string GITHUB_URL = "https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-SFRThelper";

        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                // First, initialize the AppConfig - THIS IS CRUCIAL
                // based on the AppConfig implementation you shared
                string scriptPath = Assembly.GetExecutingAssembly().Location;
                try
                {
                    // Initialize the AppConfig with the executing assembly path
                    AppConfig.GetAppConfig(scriptPath);
                    //MessageBox.Show("AppConfig initialized successfully", "Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception configEx)
                {
                    MessageBox.Show($"Failed to initialize AppConfig: {configEx.Message}\n\nPath: {scriptPath}\n\nContinuing without configuration...",
                                   "Configuration Warning",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);

                    // Create an empty dictionary as fallback if the config file can't be loaded
                    AppConfig.m_appSettings = new Dictionary<string, string>();
                }

                // Set up the EulaConfig directory
                string scriptDirectory = Path.GetDirectoryName(scriptPath);

                // EULA verification


                // Check if patient/plan is selected
                if (context.Patient == null || context.PlanSetup == null)
                {
                    MessageBox.Show("No active patient/plan selected - exiting",
                                    "MAAS-SFRTHelper",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation);
                    return;
                }

                // Continue with original expiration check
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var noexp_path = Path.Combine(path, "NOEXPIRE");
                bool foundNoExpire = File.Exists(noexp_path);

                var provider = new CultureInfo("en-US");
                var asmCa = typeof(Script).Assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(AssemblyExpirationDate));

                // Check if we have a valid expiration date and if the app is expired
                if (asmCa != null && asmCa.ConstructorArguments.Count > 0)
                {
                    DateTime endDate = DateTime.MaxValue;
                    if (DateTime.Now <= endDate )
                    {
                        // Display opening msg based on validation status

                        try
                        {
                            // Create the ESAPI worker in the main thread with careful exception handling
                            EsapiWorker esapiWorker = null;
                            try
                            {
                                esapiWorker = new EsapiWorker(context);
                            }
                            catch (Exception workerEx)
                            {
                                MessageBox.Show($"Error creating EsapiWorker: {workerEx.Message}\n\n{workerEx.StackTrace}",
                                               "Worker Initialization Error",
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Error);
                                return;
                            }

                            // This will prevent the script from exiting until the window is closed
                            DispatcherFrame frame = new DispatcherFrame();

                            // Set up the thread that will run the UI
                            Thread thread = new Thread(() =>
                            {
                                try
                                {
                                    // Make sure the AppConfig is available in this thread too
                                    if (AppConfig.m_appSettings == null)
                                    {
                                        AppConfig.GetAppConfig(scriptPath);
                                    }

                                    // Create and show the window
                                    var mainWindow = new MainWindow(esapiWorker);
                                    mainWindow.ShowDialog();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"UI Thread Error: {ex.Message}\n\n{ex.StackTrace}",
                                                   "Error in UI Thread",
                                                   MessageBoxButton.OK,
                                                   MessageBoxImage.Error);
                                }
                                finally
                                {
                                    // Always ensure the frame is released
                                    frame.Continue = false;
                                }
                            });

                            // Configure the thread properly
                            thread.SetApartmentState(ApartmentState.STA);
                            thread.IsBackground = true;
                            thread.Start();

                            // Wait until the window is closed
                            Dispatcher.PushFrame(frame);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Thread Creation Error: {ex.Message}\n\n{ex.StackTrace}",
                                           "Error Starting Application",
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Application has expired. Newer builds with future expiration dates can be found here: {GITHUB_URL}",
                                        "MAAS-SFRTHelper",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                    }
                }
                else
                {
                    // If there's no expiration date attribute, warn and continue
                    MessageBox.Show("Could not verify application expiration date. The application will continue, but may have limited functionality.",
                                    "MAAS-SFRTHelper",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);

                    // Try to launch anyway 
                    try
                    {
                        EsapiWorker esapiWorker = new EsapiWorker(context);
                        DispatcherFrame frame = new DispatcherFrame();

                        // Launch UI thread
                        Thread thread = new Thread(() =>
                        {
                            try
                            {
                                // Ensure AppConfig is initialized in this thread too
                                if (AppConfig.m_appSettings == null)
                                {
                                    AppConfig.GetAppConfig(scriptPath);
                                }

                                var mainWindow = new MainWindow(esapiWorker);
                                mainWindow.ShowDialog();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error in UI thread: {ex.Message}\n\n{ex.StackTrace}",
                                               "Error",
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Error);
                            }
                            finally
                            {
                                frame.Continue = false;
                            }
                        });

                        thread.SetApartmentState(ApartmentState.STA);
                        thread.IsBackground = true;
                        thread.Start();

                        Dispatcher.PushFrame(frame);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error launching application: {ex.Message}\n\n{ex.StackTrace}",
                                       "MAAS-SFRTHelper",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // More detailed error reporting
                MessageBox.Show($"Main Thread Error: {ex.Message}\n\n{ex.StackTrace}",
                               "MAAS-SFRTHelper Error",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
    }
}