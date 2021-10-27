using System.Linq;
using DevExpress.AspNetCore;
using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardCommon;
using DevExpress.DashboardWeb;
using DevExpress.Data.Filtering;
using DevExpress.DataAccess.Sql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WebDashboardAspNetCore.Models;

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

                DataSourceInMemoryStorage dataSourceStorage = new DataSourceInMemoryStorage();

                // Registers an SQL data source.
                DashboardSqlDataSource sqlDataSource = new DashboardSqlDataSource("Categories", "NWindConnectionString");
                SelectQuery query = SelectQueryFluentBuilder
                    .AddTable("Categories")
                    .SelectAllColumnsFromTable()
                    .Build("Categories");
                sqlDataSource.Queries.Add(query);
                dataSourceStorage.RegisterDataSource("sqlDataSource", sqlDataSource.SaveToXml());

                // Registers an Object data source.
                DashboardObjectDataSource objDataSource = new DashboardObjectDataSource("Invoices");
                dataSourceStorage.RegisterDataSource("objDataSource", objDataSource.SaveToXml());

                configurator.SetDataSourceStorage(dataSourceStorage);

                // Applies dynamic filter in the Designer working mode for the DashboardObjectDataSource.
                configurator.DataLoading += (s, e) => {
                    var contextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
                    var workingMode = contextAccessor.HttpContext.Request.Headers["DashboardWorkingMode"];

                    if (e.DataSourceName == "Invoices") {
                        var data = Invoices.CreateData();

                        if (workingMode == "Designer")
                            e.Data = data.Where(item => item.Country == "USA");
                        else
                            e.Data = data;
                    }
                };

                // Applies dynamic filter in the Designer working mode for the DashboardSqlDataSource.
                configurator.CustomFilterExpression += (s, e) => {
                    var contextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
                    var workingMode = contextAccessor.HttpContext.Request.Headers["DashboardWorkingMode"];

                    if (e.DashboardId == "dashboard1" && e.QueryName == "Categories") {
                        if (workingMode == "Designer")
                            e.FilterExpression = CriteriaOperator.Parse("[Categories.CategoryID] = 1");
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
