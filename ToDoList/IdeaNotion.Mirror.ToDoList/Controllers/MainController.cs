/*
 * Copyright (c) 2013 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except
 * in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing permissions and
 * limitations under the License.
 */

using DotNetOpenAuth.OAuth2;
using Google;
using Google.Apis.Json;
using Google.Apis.Mirror.v1;
using Google.Apis.Mirror.v1.Data;
using Google.Apis.Services;
using IdeaNotion.Mirror.ToDoList.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace IdeaNotion.Mirror.ToDoList.Controllers
{
    public class MainController : AuthRequiredController
    {
        private delegate string Operation(MainController controller);
        static readonly string CARDHTML = "<article class=\"photo\">\n  <img src=\"http://glasstodo.azurewebsites.net/content/images/todo.jpg\" width=\"100%\" height=\"100%\">\n  <div class=\"overlay-gradient-tall-dark\"/>\n  <section style=\"top:0px\">\n   <ul class=\"text-auto-size\">\n{0}</ul>\n  </section>\n  <footer>\n    <p>To Do List</p>\n  </footer>\n</article>\n";
        /// <summary>
        /// Map of supported operations.
        /// </summary>
        static private Dictionary<String, Operation> Operations =
            new Dictionary<string, Operation>()
        {
            { "insertSubscription", new Operation(InsertSubscription) },
            { "deleteSubscription", new Operation(DeleteSubscription) },
            { "insertToDoListCover", new Operation(InsertToDoListCover) },
            { "insertItem", new Operation(InsertItem) },
            { "insertItemWithAction", new Operation(InsertItemWithAction) },
            { "insertItemAllUsers", new Operation(InsertItemAllUsers) },
            { "insertContact", new Operation(InsertContact) },
            { "deleteContact", new Operation(DeleteContact) },
            { "deleteTimelineItem", new Operation(DeleteTimelineItem) },
            { "deleteAll", new Operation(DeleteAll) }
        };

        #region HTTP Request Handlers

        //
        // GET: /Root/

        public ActionResult Index()
        {
            if (!Initialize())
            {
                return Redirect(Url.Action("StartAuth", "Auth"));
            }

            IndexModel rootData = new IndexModel()
            {
                Message = Session["message"] as String
            };
            Session.Remove("message");

            var listRequest = Service.Timeline.List();
            listRequest.MaxResults = 100;
            TimelineListResponse items = listRequest.Fetch();

            if (items.Items != null)
            {
                rootData.ToDoCover = items.Items.FirstOrDefault(t => t.IsBundleCover ?? false);
                rootData.HasCover = rootData.ToDoCover != null;
                rootData.ToDoItems = items.Items.Where(t => t.IsBundleCover.HasValue ? !t.IsBundleCover.Value : true).ToList();
            }
            else 
            {
                rootData.HasCover = false;
                rootData.Message = "Let's add a To Do List timeline cover first!";
            }

            return View(rootData);
        }

        [HttpPost]
        public ActionResult Post()
        {
            if (!Initialize())
            {
                return Redirect(Url.Action("StartAuth", "Auth"));
            }

            String operation = Request.Form.Get("operation");
            String message = null;

            if (Operations.ContainsKey(operation))
            {
                message = Operations[operation](this);
            }
            else
            {
                message = "I don't know how to " + operation;
            }

            Session["message"] = message;

            return Redirect(Url.Action("Index", "Main"));
        }


        #endregion

        #region Static Mirror API Operations

        /// <summary>
        /// Inserts a new subscription.
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String InsertSubscription(MainController controller)
        {
            String collection = controller.Request.Form.Get("collection");
            if (String.IsNullOrEmpty(collection))
            {
                collection = "timeline";
            }

            Subscription subscription = new Subscription()
            {
                Collection = collection,
                UserToken = controller.UserId,
                CallbackUrl = "https://mirrornotifications.appspot.com/forward?url=" + controller.Url.Action(
                    "", "Main", null, controller.Request.Url.Scheme)
            };
            controller.Service.Subscriptions.Insert(subscription).Fetch();

            return "Application is now subscribed to updates.";
        }

        /// <summary>
        /// Deletes an existing subscription.
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String DeleteSubscription(MainController controller)
        {
            String collection = controller.Request.Form.Get("subscriptionId");

            controller.Service.Subscriptions.Delete(collection).Fetch();
            return "Application has been unsubscribed.";
        }

        /// <summary>
        /// Inserts a timeline item.
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String InsertItem(MainController controller)
        {
            TimelineItem item = new TimelineItem()
            {
                Text = controller.Request.Form.Get("message"),
                BundleId = "ToDoList",
                Notification = new NotificationConfig() { Level = "DEFAULT" },
                MenuItems = new List<MenuItem>() { 
                { new MenuItem() { Action = "DELETE", Values = new List<MenuValue> { new MenuValue { DisplayName = "REMOVE"} }  } },
                { new MenuItem() { Action = "READ_ALOUD" } },
                { new MenuItem() { Action = "REPLY", Values = new List<MenuValue> { new MenuValue { DisplayName = "ADD TODO"} }  } },
                { new MenuItem() { Action = "TOGGLE_PINNED" } },
                },
            };
            HttpPostedFileBase file = null;
            try
            {
                file = controller.Request.Files.Get("imagefile");
            }
            catch
            {
            }

            if (file != null && file.ContentLength > 0)
            {

                controller.Service.Timeline.Insert(item, file.InputStream, "image/jpeg").Upload();
            }
            else
            {
                controller.Service.Timeline.Insert(item).Fetch();
            }

            UpdateToDoCover(controller.Service);


            return "To do item has been inserted.";
        }

        private static String InsertToDoListCover(MainController controller)
        {
            TimelineItem item = new TimelineItem()
            {
                Html = string.Format(CARDHTML, ""),
                Notification = new NotificationConfig() { Level = "DEFAULT" },
                BundleId = "ToDoList",
                IsBundleCover = true,
                Title = "To Do List",
                MenuItems = new List<MenuItem>() { 
                { new MenuItem() { Action = "REPLY", Values = new List<MenuValue> { new MenuValue { DisplayName = "ADD TODO"} }  } },
                { new MenuItem() { Action = "TOGGLE_PINNED" } },
                
                },
            };


            var stream = System.IO.File.OpenRead(controller.Server.MapPath("~/Content/Images/todo.jpg"));

            controller.Service.Timeline.Insert(item, stream, "image/jpeg").Upload();

            Subscription subscription = new Subscription()
            {
                Collection = "timeline",
                UserToken = controller.UserId,
                CallbackUrl = "https://mirrornotifications.appspot.com/forward?url=" + controller.Url.Action(
                    "Notify", "Notify", null, controller.Request.Url.Scheme)
            };
            controller.Service.Subscriptions.Insert(subscription).Fetch();
            InsertContact(controller);

            UpdateToDoCover(controller.Service);

            return "Cover inserted.";
        }


        /// <summary>
        /// Inserts a timeline item with action.
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String InsertItemWithAction(MainController controller)
        {
            TimelineItem item = new TimelineItem()
            {
                Creator = new Contact()
                {
                    DisplayName = "",
                    Id = "",
                },
                Text = "Tell me what you had for lunch :)",
                Notification = new NotificationConfig() { Level = "DEFAULT" },
                MenuItems = new List<MenuItem>() { { new MenuItem() { Action = "REPLY" } } },
            };
            controller.Service.Timeline.Insert(item).Fetch();
            UpdateToDoCover(controller.Service);

            return "A timeline item with action has been inserted.";
        }

        public static void UpdateToDoCover(MirrorService service)
        {
            var listRequest = service.Timeline.List();
            listRequest.MaxResults = 100;
            TimelineListResponse items = listRequest.Fetch();
            var cover = items.Items.FirstOrDefault(t => t.IsBundleCover ?? false);
            var content = "";
            if (cover != null)
            {
                foreach(var i in items.Items.Where(t => t.IsBundleCover.HasValue ? !t.IsBundleCover.Value : true).ToList()) {
                    content += "<li>" + i.Text + "</li>\n";
                }
                cover.Html = string.Format(CARDHTML, content);
            }

            service.Timeline.Update(cover, cover.Id).Fetch();
        }

        /// <summary>
        /// Inserts a timeline item to all users (up to 10).
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String InsertItemAllUsers(MainController controller)
        {
            StoredCredentialsDBContext db = new StoredCredentialsDBContext();
            int userCount = db.StoredCredentialSet.Count();

            if (userCount > 10)
            {
                return "Total user count is " + userCount +
                    ". Aborting broadcast to save your quota";
            }
            else
            {
                TimelineItem item = new TimelineItem()
                {
                    Text = "Hello Everyone!",
                    Notification = new NotificationConfig() { Level = "DEFAULT" }
                };

                foreach (StoredCredentials creds in db.StoredCredentialSet)
                {
                    AuthorizationState state = new AuthorizationState()
                    {
                        AccessToken = creds.AccessToken,
                        RefreshToken = creds.RefreshToken
                    };
                    MirrorService service = new MirrorService(new BaseClientService.Initializer()
                    {
                        Authenticator = Utils.GetAuthenticatorFromState(state)
                    });
                    service.Timeline.Insert(item).Fetch();
                }
                return "Successfully sent cards to " + userCount + " users.";
            }
        }

        /// <summary>
        /// Inserts a sharing contact.
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String InsertContact(MainController controller)
        {
            String imageUrl = "http://glasstodo.azurewebsites.net/content/images/todo.jpg";

            Contact contact = new Contact()
            {
                DisplayName = "To Do List",
                Id = "ToDoList",
                ImageUrls = new List<String>() { imageUrl }
            };

            controller.Service.Contacts.Insert(contact).Fetch();

            return "Inserted contact.";
        }

        /// <summary>
        /// Deletes a sharing contact.
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String DeleteContact(MainController controller)
        {
            controller.Service.Contacts.Delete(controller.Request.Form.Get("id")).Fetch();
            return "Contact has been deleted.";
        }

        /// <summary>
        /// Deletes a timeline item.
        /// </summary>
        /// <param name="controller">Controller calling this method.</param>
        /// <returns>Status message.</returns>
        private static String DeleteTimelineItem(MainController controller)
        {
            controller.Service.Timeline.Delete(controller.Request.Form.Get("itemId")).Fetch();
            UpdateToDoCover(controller.Service);
            return "A timeline item has been deleted.";
        }

        
        private static String DeleteAll(MainController controller)
        {
            var listRequest = controller.Service.Timeline.List();
            listRequest.MaxResults = 100;
            TimelineListResponse items = listRequest.Fetch();
            foreach (var i in items.Items)
            {
                controller.Service.Timeline.Delete(i.Id).Fetch();
            }

            return "All data removed...";
        }

        #endregion
    }
}
