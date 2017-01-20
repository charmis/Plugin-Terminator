using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using Plugin_Terminator.LoginWindow;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Plugin_Terminator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CrmServiceClient _svcClient;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoginToCRM();
        }

        /// <summary>
        /// Raised when the login form process is completed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ctrl_ConnectionToCrmCompleted(object sender, EventArgs e)
        {
            if (sender is CrmLogin)
            {
                this.Dispatcher.Invoke(() =>
                {
                    ((CrmLogin)sender).Close();
                });
            }
        }

        private void btnListPlugins_Click(object sender, RoutedEventArgs e)
        {
            ListPluginAssemblies();
        }

        private void ListPluginAssemblies()
        {
            var pluginAssemblies = getAllPluginAssemblies();
            dgPluginAssemblies.ItemsSource = pluginAssemblies.Select(p => new
            {
                Id = p.Id,
                Name = p.GetAttributeValue<string>("name")
            });
        }

        private DataCollection<Entity> getAllPluginAssemblies()
        {
            QueryExpression userSettingsQuery = new QueryExpression("pluginassembly");
            userSettingsQuery.ColumnSet.AllColumns = false;
            userSettingsQuery.ColumnSet.AddColumn("name");

            userSettingsQuery.Criteria = new FilterExpression
            {
                FilterOperator = LogicalOperator.And,
                Conditions ={
                    new ConditionExpression
                    {
                        AttributeName = "name",
                        Operator = ConditionOperator.DoesNotBeginWith,
                        Values = { "Microsoft" }
                    }
                }
            };

            var retrieveRequest = new RetrieveMultipleRequest()
            {
                Query = userSettingsQuery
            };

            EntityCollection EntCol = null;

            if (_svcClient.IsReady)
            {
                var response = _svcClient.ExecuteCrmOrganizationRequest(retrieveRequest) as RetrieveMultipleResponse;

                EntCol = response.EntityCollection;
            }

            return EntCol?.Entities;
        }

        private void DeletePlugin(Guid selectedPluginAssemblyId)
        {
            Stopwatch _totalTimeElapsed = Stopwatch.StartNew();

            var pluginTypes = GetPluginTypes(selectedPluginAssemblyId);

            DeletePluginTypes(pluginTypes);

            DeletePluginAssembly(selectedPluginAssemblyId);

            _totalTimeElapsed.Stop();

            Log($"Time elapsed : {_totalTimeElapsed.Elapsed.TotalSeconds} seconds.");

            ListPluginAssemblies();
        }

        private EntityCollection GetPluginTypes(Guid pluginAssemblyId)
        {
            var pluginTypeQuery = new QueryExpression
            {
                EntityName = "plugintype",
                Criteria =
                {
                    FilterOperator = LogicalOperator.And,
                    Conditions ={
                                    new ConditionExpression
                                        {
                                            AttributeName = "pluginassemblyid",
                                            Operator = ConditionOperator.Equal,
                                            Values = { pluginAssemblyId }
                                        },
                                }
                }
            };

            var retrieveMultipleRequest = new RetrieveMultipleRequest()
            {
                Query = pluginTypeQuery
            };
            EntityCollection EntCol = null;

            if (_svcClient.IsReady)
            {
                EntCol = (_svcClient.ExecuteCrmOrganizationRequest(retrieveMultipleRequest) as RetrieveMultipleResponse).EntityCollection;
            }

            return EntCol;
        }

        private void DeletePluginTypes(EntityCollection pluginTypes)
        {
            foreach (var pluginType in pluginTypes.Entities)
            {
                DeletePluginSteps(pluginType.Id);

                _svcClient.DeleteEntity("plugintype", pluginType.Id);
            }
        }

        private void DeletePluginSteps(Guid pluginTypeId)
        {
            var pluginSteps = GetPluginSteps(pluginTypeId);

            if (pluginSteps.Entities.Count <= 0)
            {
                return;
            }

            foreach (var pluginStep in pluginSteps.Entities)
            {
                _svcClient.DeleteEntity(pluginStep.LogicalName, pluginStep.Id);
            }
        }

        private void DeletePluginAssembly(Guid pluginAssemblyId)
        {
            if (pluginAssemblyId != null)
            {
                _svcClient.DeleteEntity("pluginassembly", pluginAssemblyId);
            }
        }

        private EntityCollection GetPluginSteps(Guid pluginTypeId)
        {
            var pluginStepQuery = new QueryExpression
            {
                EntityName = "sdkmessageprocessingstep",
                Criteria =
                {
                    FilterOperator = LogicalOperator.And,
                    Conditions = {
                                    new ConditionExpression
                                        {
                                            AttributeName = "plugintypeid",
                                            Operator = ConditionOperator.Equal,
                                            Values = {pluginTypeId}
                                        },
                                }
                }
            };

            var retrieveMultipleRequest = new RetrieveMultipleRequest()
            {
                Query = pluginStepQuery
            };

            EntityCollection EntCol = (_svcClient.ExecuteCrmOrganizationRequest(retrieveMultipleRequest) as RetrieveMultipleResponse).EntityCollection;

            return EntCol;
        }

        private async void btnDeletePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (dgPluginAssemblies.SelectedIndex == -1)
            {
                MessageBox.Show("A plugin must be selected to delete.");
                return;
            }
            else
            {
                ClearLog();

                dynamic selectedPlugin = dgPluginAssemblies.SelectedItem;

                MessageBoxResult messageBoxResult = MessageBox.Show($"Do you want to delete the selected plugin {selectedPlugin.Name} in Org {_svcClient.ConnectedOrgFriendlyName} in server {_svcClient.CrmConnectOrgUriActual.Host}?", "Delete Confirmation", System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.No)
                {
                    return;
                }

                Log($"Plugin Selected : {selectedPlugin.Name}");
                Log("Delete Plugin - Started...");

                Guid pluginId = selectedPlugin.Id;
                await Task.Run(() => DeletePlugin(pluginId));
                //deleteTask.Wait();
                Log("Delete Plugin - Completed.");
            }
        }

        private void Log(string logMessage)
        {
            txtLog.Text += logMessage + Environment.NewLine;
        }

        private void ClearLog()
        {
            txtLog.Text = string.Empty;
        }

        private void mnuLogin_Click(object sender, RoutedEventArgs e)
        {
            LoginToCRM();
        }

        private void LoginToCRM()
        {
            // Establish the Login control
            CrmLogin ctrl = new CrmLogin();
            // Wire Event to login response. 
            ctrl.ConnectionToCrmCompleted += ctrl_ConnectionToCrmCompleted;
            // Show the dialog. 
            ctrl.ShowDialog();

            if (ctrl.CrmConnectionMgr != null && ctrl.CrmConnectionMgr.CrmSvc != null && ctrl.CrmConnectionMgr.CrmSvc.IsReady)
            {
                _svcClient = ctrl.CrmConnectionMgr.CrmSvc;

                ((MainWindow)Application.Current.MainWindow).Title = $"Plugin Terminator : Connected to {_svcClient.CrmConnectOrgUriActual.Host} : {_svcClient.ConnectedOrgFriendlyName}";
            }
        }

        private void mnuListAssemblies_Click(object sender, RoutedEventArgs e)
        {
            //WhoAmIRequest request = new WhoAmIRequest();
            //WhoAmIResponse response = null;
            //response = _svcClient.ExecuteCrmOrganizationRequest(request) as WhoAmIResponse;

            ListPluginAssemblies();
        }
    }
}