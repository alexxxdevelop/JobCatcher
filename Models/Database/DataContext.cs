using System;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Configuration;

namespace JobCatcher
{
    public class DataContext : DbContext
    {
        public DbSet<Setting> Settings { get; set; }
        public DbSet<Vacancy> Vacancies { get; set; }

        public DataContext()
        {
            this.Database.Connection.ConnectionString = ConfigurationManager.ConnectionStrings["connString"].ConnectionString;
            System.Data.Entity.Database.SetInitializer(new MigrateDatabaseToLatestVersion<DataContext, Configuration>());
        }
    }

    public sealed class Configuration : DbMigrationsConfiguration<DataContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(DataContext context)
        {
            base.Seed(context);
        }
    }
}
