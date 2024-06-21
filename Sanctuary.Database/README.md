> Note:
> Open Package Manager Console
> Change the default project to your database provider. Eg: `MySql`

# Create your first migration

## Add a migration

```powershell
Add-Migration Initial -Startup Sanctuary.Database
```

## Remove a migration

```powershell
Remove-Migration
```

# Create your database and schema

## Updating the database
```powershell
Update-Database -Startup Sanctuary.Database
```

## Additional resources
[Managing Migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations)