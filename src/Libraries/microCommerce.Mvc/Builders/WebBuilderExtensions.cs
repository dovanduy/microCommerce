﻿using Hangfire;
using microCommerce.Common;
using microCommerce.Ioc;
using microCommerce.Logging;
using microCommerce.Mvc.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace microCommerce.Mvc.Builders
{
    public static class WebBuilderExtensions
    {
        public static void ConfigurePipeline(this IApplicationBuilder app, IHostingEnvironment env)
        {
            //use gzip compression
            app.UseResponseCompression();

            //exception handling
            app.UseCustomExceptionHandler(env);

            //not found handling
            app.UseCustomPageNotFound();

            //use session storage
            app.UseSession();

            var fileProvider = IocContainer.Current.Resolve<ICustomFileProvider>();

            //use static files
            app.UseStaticFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(fileProvider.MapPath("Themes")),
                RequestPath = new PathString("/Themes")
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(fileProvider.MapPath("Modules")),
                RequestPath = new PathString("/Modules")
            });

            //use mvc engine
            app.UseMvc(RegisterRoutes);

            app.UseHangfireServer();
            app.UseHangfireDashboard();

            //set culture by user data
            app.UseCulture();
        }

        private static void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            var assemblyHelper = IocContainer.Current.Resolve<IAssemblyHelper>();
            var routeProviders = assemblyHelper.FindOfType<IRouteProvider>();

            var instances = routeProviders
                .Select(rp => (IRouteProvider)Activator.CreateInstance(rp))
                .OrderBy(rp => rp.Priority);

            foreach (var instance in instances)
                instance.RegisterRoutes(routeBuilder);

            routeBuilder.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
        }

        private static void UseCustomExceptionHandler(this IApplicationBuilder application, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                //get detailed exceptions for developing and testing purposes
                application.UseDeveloperExceptionPage();
            }
            else
            {
                //or use special exception handler
                application.UseExceptionHandler("/error.html");
            }

            //log errors
            application.UseExceptionHandler(handler =>
            {
                handler.Run(context =>
                {
                    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                    if (exception == null)
                        return Task.CompletedTask;

                    try
                    {
                        var logger = IocContainer.Current.Resolve<ILogger>();
                        var webHelper = IocContainer.Current.Resolve<IWebHelper>();
                        logger.Error(exception.Message, exception, webHelper.GetCurrentIpAddress(), webHelper.GetThisPageUrl(true), webHelper.GetUrlReferrer());
                    }
                    finally
                    {
                        //rethrow the exception to show the error page
                        throw exception;
                    }
                });
            });
        }

        private static void UseCustomPageNotFound(this IApplicationBuilder application)
        {
            application.UseStatusCodePages(async context =>
            {
                //handle 404 Not Found
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status404NotFound)
                {
                    var webHelper = IocContainer.Current.Resolve<IWebHelper>();
                    if (webHelper.IsStaticResource("text/html") || !webHelper.IsStaticResource())
                    {
                        //get new path
                        context.HttpContext.Request.Path = "/notfound.html";
                        context.HttpContext.Request.QueryString = QueryString.Empty;

                        //re-execute request with new path
                        await context.Next(context.HttpContext);
                    }
                }
            });
        }
    }
}