using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace IdeaNotion.Mirror.ToDoList
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "auth", // Route name
                "auth", // URL with parameters
                new { controller = "Auth", action = "StartAuth" } // Parameter defaults
            );

            routes.MapRoute(
                "oauth2callback", // Route name
                "oauth2callback", // URL with parameters
                new { controller = "Auth", action = "OAuth2Callback" } // Parameter defaults
            );

            routes.MapRoute(
                "signout", // Route name
                "signout", // URL with parameters
                new { controller = "Auth", action = "Signout" } // Parameter defaults
            );

            routes.MapRoute(
                "attachmentproxy", // Route name
                "attachmentproxy", // URL with parameters
                new { controller = "AttachmentProxy", action = "GetAttachment" } // Parameter defaults
            );

            routes.MapRoute(
                "notify", // Route name
                "notify", // URL with parameters
                new { controller = "Notify", action = "Notify" } // Parameter defaults
            );

            routes.MapRoute(
                "post", // Route name
                "post", // URL with parameters
                new { controller = "Main", action = "Post" } // Parameter defaults
            );

            routes.MapRoute(
                "default", // Route name
                "", // URL with parameters
                new { controller = "Main", action = "Index" } // Parameter defaults
            );
        }
    }
}