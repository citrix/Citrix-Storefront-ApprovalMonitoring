using Citrix.DeveloperNetwork.StoreFront;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Quobject.SocketIoClientDotNet.Client;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using System.Web;
using System.Windows.Threading;

namespace SFWorkflowService
{
    public partial class Form1 : Form
    {
        Timer _watchTimer = null;
        SubscriptionStore _store = null;
        //Enter your storefront server subscription store local here. An example is below
        string _storeURL = @"net.pipe://localhost/Citrix/Subscriptions/1__Citrix_store";
        //Meshblu endpoint. You can leave this.
        string _meshbluEnbpoint = "wss://meshblu.octoblu.com";
        //substitute you own workflow url here. You can get it from the trigger node "HTTP Post"
        //value of you workflow. You can also check out our video on calling a trigger from a 
        //.net application here https://www.youtube.com/watch?v=Se4NRps9Gh8
        string _workflowEndpoint = "https://triggers.octoblu.com/flows/{flowid}/triggers/{trigger node id}";
        //If you need help with creating a UUID/token within a .NET application check out
        //our live coding video on doing just there here - https://www.youtube.com/watch?v=eT92moezKic
        string _deviceUUID = "[Please enter your device/application UUID]";
        string _token = "[Please enter your token]";
        bool _queriedInitialSubscriptions = false;

        public Form1()
        {
            InitializeComponent();

            SetupSocketListener(_deviceUUID, _token);
        }

        private void SetupSocketListener(string UUID, string Token)
        {
            var _socket = IO.Socket(_meshbluEnbpoint, new IO.Options { Port = 443 });
            _socket.On("connect", () =>
            {
                Console.WriteLine("Connect");
                _socket.On("identify", (data) =>
                {
                    Console.WriteLine("Sending device UUID to meshblu");
                    var _identity_data = new JObject();
                    _identity_data.Add("uuid", UUID);
                    _identity_data.Add("socketid", "data.socketid");
                    _identity_data.Add("token", Token);
                    _socket.Emit("identity", _identity_data);
                });
            });
            _socket.On("ready", (dynamic data) =>
            {
                this.textBox1.Invoke(new Action(() =>
                {
                    this.textBox1.Text = string.Format("{0}\r\n{1}", "Device Ready", this.textBox1.Text);
                }), null);

                if (data.status == 201)
                {
                    var _ready_data = new JObject();
                    _ready_data.Add("uuid", UUID);
                    _ready_data.Add("token", Token);
                    _socket.Emit("subscribe", _ready_data);
                }

            });
            _socket.On("notReady", (data) => {
                Console.WriteLine("Not Ready");
            });

            _socket.On("message", (data) =>
            {
                Console.WriteLine(data);

                JObject _meshbluMessage = JObject.Parse(data.ToString());
                Classes.WorkflowApplicationResponse _workflowResponse = JsonConvert.DeserializeObject<Classes.WorkflowApplicationResponse>(_meshbluMessage["payload"].ToString());

                if (_workflowResponse.status != null)
                {
                    this.textBox1.Invoke(new Action(() =>
                    {
                        this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Message Received from meshblu", data, this.textBox1.Text);
                    }), null);

                    this.textBox1.Invoke(new Action(() =>
                    {
                        this.textBox1.Text = string.Format("{0}{1}\r\n{2}", "Workflow Message:", _workflowResponse.status, this.textBox1.Text);
                        this.textBox1.Text = string.Format("{0}{1}\r\n{2}", "Workflow Message:", _workflowResponse.user, this.textBox1.Text);
                        this.textBox1.Text = string.Format("{0}{1}\r\n{2}", "Workflow Message:", _workflowResponse.application, this.textBox1.Text);
                    }), null);

                    switch (_workflowResponse.status.ToLower())
                    {
                        case "approved":
                            ApproveApplication(_workflowResponse);
                            break;
                        case "deny":
                            DenyApplication(_workflowResponse);
                            break;
                        default:
                            break;
                    }
                }
            });
        }
        private void ApproveApplication(Classes.WorkflowApplicationResponse ApplicationInfo)
        {
            try
            {
                this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Approval Message", ApplicationInfo.application, this.textBox1.Text);

                string _user = ApplicationInfo.user.Replace("//", @"\");
                this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Querying SF for app", ApplicationInfo.application, this.textBox1.Text);
                var _applicationSubscription = _store.GetSubscriptionInfo(_user, ApplicationInfo.application);
                if (_applicationSubscription != null)
                {
                    this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Subscription is not null", ApplicationInfo.application, this.textBox1.Text);
                    this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Found app", ApplicationInfo.application, this.textBox1.Text);
                    this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Updating app status", ApplicationInfo.application, this.textBox1.Text);
                    _applicationSubscription.Status = SubscriptionStatus.subscribed;
                    this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Saving status", ApplicationInfo.application, this.textBox1.Text);
                    _store.SetSubscriptionInfo(_applicationSubscription);
                    _store.SaveChanges();
                }
            }
            catch(Exception e)
            {
                this.textBox1.Text = string.Format("{0}\r\n{1}", "Error", e.Message);
            }
        }
        private void DenyApplication(Classes.WorkflowApplicationResponse ApplicationInfo)
        {
            var _applicationSubscription = _store.RemoveSubscriptionInfo(ApplicationInfo.user, ApplicationInfo.application);
            _store.SaveChanges();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if ( textBox1.Text.Trim() == "")
            {
                MessageBox.Show("You must enter a managers email for approval");
                return;
            }

            button1.Enabled = false;
            button2.Enabled = true;

            this._watchTimer = new Timer();
            this._watchTimer.Interval = 15000;
            this._watchTimer.Tick += async (timer_sender, eventargs) =>
            {
                IEnumerable<SubInfo> _subs = null;
                if ( _queriedInitialSubscriptions == false)
                {
                    //query all subscriptions
                    _subs = _store.GetAllSubscriptions();
                    _queriedInitialSubscriptions = true;
                }
                else
                {
                    // only grab the new subscriptions since the last time we queried. (The dll handles this)
                    _subs = _store.GetNewSubscriptions();
                }

                foreach (var _subInfo in _subs)
                {
                    if (_subInfo.Status == SubscriptionStatus.pending)
                    {
                        this.textBox1.Text = string.Format("{0}\r\n{1}", "Found subscription", this.textBox1.Text);

                        //call the octoblu workflow
                        string _user = _subInfo.User;
                        string _resourceName = _subInfo.Resource;
                        string _status = _subInfo.Status.ToString();

                        this.textBox1.Text = string.Format("Subscription Info: {0}\r\n{1}", _subInfo.Sid, this.textBox1.Text);

                        Classes.WorkflowApplicationRequest _wfInfo = new Classes.WorkflowApplicationRequest
                        {
                            type = "request",
                            user = _user.Replace("\\", "//"),
                            application = HttpUtility.HtmlEncode(_resourceName),
                            monitordeviceid = _deviceUUID,
                            manageremail = txtManagerEmail.Text
                        };

                        this.textBox1.Text = string.Format("{0}\r\n{1}", "Posting to the Octoblu endpoint", this.textBox1.Text);
                        this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Data to endpoint:", Newtonsoft.Json.JsonConvert.SerializeObject(_wfInfo), this.textBox1.Text);
                        //post to the endpoint
                        HttpClient _client = new HttpClient();
                        StringContent _bodyJson = new StringContent(
                            Newtonsoft.Json.JsonConvert.SerializeObject(_wfInfo),
                            UTF8Encoding.UTF8,
                            "application/json");

                        HttpResponseMessage _postResp = await _client.PostAsync(_workflowEndpoint, _bodyJson);
                        if (_postResp.StatusCode == System.Net.HttpStatusCode.Created)
                        {
                            this.textBox1.Text = string.Format("{0}\r\n{1}", "Post was successful", this.textBox1.Text);
                        }
                        else
                        {
                            this.textBox1.Text = string.Format("{0}\r\n{1}\r\n{2}", "Something happened - Message:", _postResp.ReasonPhrase, this.textBox1.Text);
                        }
                    }
                }
            };

            //start the timer
            this._watchTimer.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this._watchTimer.Stop();

            button1.Enabled = true;
            button2.Enabled = false;

        }
    }
}
