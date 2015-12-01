using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace NServiceBus.Backplane.SqlServer
{
    class SqlServerDataBackplane : IDataBackplane
    {
        private readonly string owner;
        readonly string connectionString;

        public SqlServerDataBackplane(string owner, string connectionString)
        {
            this.owner = owner;
            this.connectionString = connectionString;
        }

        public async Task Publish(string type, string data)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    using (var command = new SqlCommand(@"
UPDATE [Data] SET [Value] = @Value WHERE [Owner] = @Owner AND [Type] = @Type
IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO [Data] ([Owner], [Type], [Value]) VALUES (@Owner, @Type, @Value)
END
", connection, transaction))
                    {
                        command.Parameters.AddWithValue("Owner", owner).DbType = DbType.AnsiString;
                        command.Parameters.AddWithValue("Type", type).DbType = DbType.AnsiString;
                        command.Parameters.AddWithValue("Value", data).DbType = DbType.String;
                        await command.ExecuteNonQueryAsync();
                    }
                    transaction.Commit();
                }
            }
        }

        public async Task Revoke(string type)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = new SqlCommand(@"
DELETE FROM [Data] WHERE [Owner] = @Owner AND [Type] = @Type
", connection, transaction))
                    {
                        command.Parameters.AddWithValue("Owner", owner).DbType = DbType.AnsiString;
                        command.Parameters.AddWithValue("Type", type).DbType = DbType.AnsiString;
                        await command.ExecuteNonQueryAsync();
                    }
                    transaction.Commit();
                }
            }
        }

        public async Task<IReadOnlyCollection<Entry>> Query()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = new SqlCommand(@"
SELECT [Owner], [Type], [Value] FROM [Data] WHERE [Owner] <> @Owner
", connection, transaction))
                    {
                        command.Parameters.AddWithValue("Owner", owner).DbType = DbType.AnsiString;

                        var results = new List<Entry>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                results.Add(new Entry((string)reader[0], (string)reader[1], (string)reader[2]));
                            }
                        }
                        return results;
                    }
                }
            }
        }
    }
}
