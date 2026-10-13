-- Deploy Mahkias ERP schema to Azure SQL.
-- From Database\Scripts:
--   sqlcmd -S mahkiaserpdb-server.database.windows.net -d mahkias -U mahkias_admin -I -i 00_Deploy.sql
:r .\Tables\01_CreateTables.sql
:r .\Tables\02_DefaultData.sql
:r .\UserDefinedTypes\CreateUserDefinedTypes.sql
:r .\Functions\NaturalSortKey.sql
:r .\Functions\SplitAlternativePartNumbers.sql
:r .\StoredProcedures\BulkInsertActivities.sql
:r .\StoredProcedures\CreateActivity.sql
:r .\StoredProcedures\CreateProject.sql
:r .\StoredProcedures\CreateProjectDocument.sql
:r .\StoredProcedures\CreateQuotation.sql
:r .\StoredProcedures\CreateQuotationDocument.sql
:r .\StoredProcedures\DeleteActivity.sql
:r .\StoredProcedures\DeleteProject.sql
:r .\StoredProcedures\DeleteQuotationDocument.sql
:r .\StoredProcedures\GetActivitiesByProject.sql
:r .\StoredProcedures\GetProjectDriveFolder.sql
:r .\StoredProcedures\ListProjectDocuments.sql
:r .\StoredProcedures\ListQuotationDocuments.sql
:r .\StoredProcedures\ReplaceQuotationItems.sql
:r .\StoredProcedures\RestoreSoftDeleted.sql
:r .\StoredProcedures\SaveActivityAlternativeParts.sql
:r .\StoredProcedures\SearchProjects.sql
:r .\StoredProcedures\SetProjectDriveFolder.sql
:r .\StoredProcedures\SetProjectProcurementFolder.sql
:r .\StoredProcedures\UpdateActivities.sql
:r .\StoredProcedures\UpdateActivity.sql
:r .\StoredProcedures\UpdateProject.sql
:r .\StoredProcedures\UpdateQuotationDocument.sql
