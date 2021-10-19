using DevExpress.AspNetCore;
using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardWeb;
using DevExpress.Data.Filtering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;

namespace WebDashboardAspNetCore {
    public class Startup {
        public Startup(IConfiguration configuration, IWebHostEnvironment hostingEnvironment) {
            Configuration = configuration;
            FileProvider = hostingEnvironment.ContentRootFileProvider;
        }

        public IConfiguration Configuration { get; }
        public IFileProvider FileProvider { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            // Configures services to use the Web Dashboard Control.
            services
                .AddHttpContextAccessor()
                .AddDevExpressControls()
                .AddControllersWithViews();

            services.AddScoped<DashboardConfigurator>((System.IServiceProvider serviceProvider) => {
                DashboardConfigurator configurator = new DashboardConfigurator();
                configurator.SetDashboardStorage(new DashboardFileStorage(FileProvider.GetFileInfo("Data/Dashboards").PhysicalPath));
                configurator.SetConnectionStringsProvider(new DashboardConnectionStringsProvider(Configuration));

                // Disable data caching
                configurator.CustomParameters += (s, e) => {
                    e.Parameters.Add(new DevExpress.DataAccess.Parameter("NoCache", typeof(Guid), Guid.NewGuid()));
                };

                configurator.CustomFilterExpression += (s, e) => {
                    var contextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
                    var workingMode = contextAccessor.HttpContext.Request.Headers["DashboardWorkingMode"];

                    if (e.DashboardId == "dashboard1" && e.QueryName == "Categories") {
                        if (workingMode == "Designer")
                            e.FilterExpression = CriteriaOperator.Parse("[Categories.CategoryID] = 1");
                    }

                    if (e.DashboardId == "dashboard1" && e.QueryName == "Invoices") {
                        if (workingMode == "Designer")
                            e.FilterExpression = CriteriaOperator.Parse("[ID] = 1");
                    }
                };

                return configurator;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if(env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            } else {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            // Registers the DevExpress middleware.
            app.UseDevExpressControls();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => {
                // Maps the dashboard route.
                EndpointRouteBuilderExtension.MapDashboardRoute(endpoints, "api/dashboard", "DefaultDashboard");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
