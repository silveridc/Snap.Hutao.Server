using Microsoft.EntityFrameworkCore.Metadata;
using System.Text.RegularExpressions;

namespace server.Models.Context;

internal static class SnakeCaseNamingConvention
{
    private static readonly Regex WordBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.GetTableName() is { } tableName)
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.GetColumnName() is { } columnName)
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }

            foreach (IMutableIndex index in entityType.GetIndexes())
            {
                if (index.GetDatabaseName() is { } indexName)
                {
                    index.SetDatabaseName(ToSnakeCase(indexName));
                }
            }

            foreach (IMutableForeignKey foreignKey in entityType.GetForeignKeys())
            {
                if (foreignKey.GetConstraintName() is { } constraintName)
                {
                    foreignKey.SetConstraintName(ToSnakeCase(constraintName));
                }
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        return WordBoundary.Replace(value, "$1_$2").ToLowerInvariant();
    }
}
