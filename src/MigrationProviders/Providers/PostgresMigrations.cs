using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using CentridNet.EFCoreAutoMigrator.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CentridNet.EFCoreAutoMigrator.MigrationContexts{

    public static class PostgresMigrationsExtensions{
        public static MigrationsProvider PostgreSQLDBMigrations(this DbContext dbContext, DBMigratorProps dbMigratorProps, MigrationScriptExecutor migrationScriptExecutor){   
            return new PostgresMigrations(dbMigratorProps, migrationScriptExecutor);
        }
    }
    public class PostgresMigrations : MigrationsProvider
    {
        public PostgresMigrations(DBMigratorProps dbMigratorProps, MigrationScriptExecutor migrationScriptExecutor) : base(dbMigratorProps, migrationScriptExecutor){}
        protected override void EnsureMigrateTablesExist()
        {

            DataTable resultDataTable = dbContext.ExecuteSqlRawWithoutModel($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND  table_name = '{DalConsts.MIGRATION_TABLE_NAME}');");

            if (resultDataTable.Rows.Count == 0 || !(bool)resultDataTable.Rows[0][0]){
                migrationScriptExecutor.AddSQLCommand($@"CREATE TABLE {DalConsts.MIGRATION_TABLE_NAME} (
                    runId SERIAL PRIMARY KEY,
                    runDate TIMESTAMP,
                    efcoreVersion VARCHAR (355) NOT NULL,
                    metadata TEXT,
                    snapshot BYTEA NOT NULL
                );");
            }
        }

        protected override void EnsureSnapshotLimitNotReached(){
            if (snapshotHistoryLimit > 0){
                migrationScriptExecutor.AddSQLCommand($"DELETE FROM {DalConsts.MIGRATION_TABLE_NAME} WHERE runId NOT IN (SELECT runId FROM {DalConsts.MIGRATION_TABLE_NAME} ORDER BY runId DESC LIMIT {snapshotHistoryLimit-1});");
            }  
        }

        protected override AutoMigratorTable GetLastMigrationRecord()
        {
            IList<AutoMigratorTable> migrationMetadata = dbContext.ExecuteSqlRawWithoutModel<AutoMigratorTable>($"SELECT * FROM {DalConsts.MIGRATION_TABLE_NAME} ORDER BY runId DESC LIMIT 1;", (dbDataReader) => {
                return new AutoMigratorTable(){
                    runId = (int)dbDataReader[0],
                    runDate = (DateTime)dbDataReader[1],
                    efcoreVersion = (string)dbDataReader[2],
                    metadata = (string)dbDataReader[3],
                    snapshot = (byte[])dbDataReader[4]
                };
            });

            if (migrationMetadata.Count >0){
                return migrationMetadata[0];
            }
            return null;
        }

        protected override void UpdateMigrationTables(byte[] snapshotData)
        {

            migrationScriptExecutor.AddSQLCommand($@"INSERT INTO {DalConsts.MIGRATION_TABLE_NAME}  (
                                                    runDate,
                                                    efcoreVersion,
                                                    metadata,
                                                    snapshot
                                                    ) 
                                                    VALUES
                                                    (NOW(),
                                                    '{typeof(DbContext).Assembly.GetName().Version.ToString()}',
                                                    '{migrationMetadata.metadata}',
                                                     decode('{BitConverter.ToString(snapshotData).Replace("-", "")}', 'hex'));");

            
        }
    }
} 