// PerfectWorldManager.Core/DatabaseService.cs
using MySql.Data.MySqlClient;
using PerfectWorldManager.Core.Utils; // For PasswordHelper
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace PerfectWorldManager.Core
{
    public class UserAccountInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }
        public bool IsGm { get; set; }
    }

    // Add this class to represent basic character info
    public class PlayerCharacterInfo
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class DatabaseService : IDisposable
    {
        private readonly Settings _settings;
        private MySqlConnection? _connection;

        public DatabaseService(Settings settings)
        {
            _settings = settings;
        }

        private async Task OpenConnectionAsync()
        {
            if (_connection == null)
            {
                string connStr = $"server={_settings.MySqlHost};port={_settings.MySqlPort};" +
                                 $"user={_settings.MySqlUser};password={_settings.MySqlPassword};" +
                                 $"database={_settings.MySqlDatabase};charset=utf8;Allow User Variables=true;";
                _connection = new MySqlConnection(connStr);
            }

            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }
        }

        private async Task CloseConnectionAsync()
        {
            if (_connection != null && _connection.State == ConnectionState.Open)
            {
                await _connection.CloseAsync();
            }
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            await OpenConnectionAsync();
            using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM users WHERE name = @username_param", _connection))
            {
                cmd.Parameters.AddWithValue("@username_param", username.ToLower());
                object? result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
        }

        public async Task<(int id, string storedPassword, string email)> GetUserDetailsAsync(string username)
        {
            await OpenConnectionAsync();
            using (var cmd = new MySqlCommand("SELECT ID, passwd, email FROM users WHERE name = @username_param", _connection))
            {
                cmd.Parameters.AddWithValue("@username_param", username.ToLower());
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return (reader.GetInt32("ID"),
                                reader.IsDBNull(reader.GetOrdinal("passwd")) ? string.Empty : reader.GetString("passwd"),
                                reader.IsDBNull(reader.GetOrdinal("email")) ? string.Empty : reader.GetString("email"));
                    }
                }
            }
            return (-1, string.Empty, string.Empty); // User not found
        }

        [Obsolete("This method should now be called via DaemonGrpcService.")]
        public async Task<string> CreateAccountAsync(string username, string password, string email)
        {
            username = username.ToLower();
            email = (email ?? string.Empty).ToLower();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return "Username and password cannot be empty.";
            if (username.Length < 4 || username.Length > 10 || password.Length < 4 || password.Length > 10)
                return "Username/Password must be 4-10 characters.";
            if (!PasswordHelper.IsValidCharacterSet(username) || !PasswordHelper.IsValidCharacterSet(password))
                return "Username/Password contains forbidden characters.";

            if (await UserExistsAsync(username))
                return "User already exists.";

            string hashedPassword = PasswordHelper.PwEncode(username, password);

            try
            {
                await OpenConnectionAsync();
                using (var cmd = new MySqlCommand("adduser", _connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.AddWithValue("@name1", username);
                    cmd.Parameters.AddWithValue("@passwd1", hashedPassword);
                    cmd.Parameters.AddWithValue("@prompt1", "0");
                    cmd.Parameters.AddWithValue("@answer1", "0");
                    cmd.Parameters.AddWithValue("@truename1", "0");
                    cmd.Parameters.AddWithValue("@idnumber1", "0");
                    cmd.Parameters.AddWithValue("@email1", email);
                    cmd.Parameters.AddWithValue("@mobilenumber1", "0");
                    cmd.Parameters.AddWithValue("@province1", "0");
                    cmd.Parameters.AddWithValue("@city1", "0");
                    cmd.Parameters.AddWithValue("@phonenumber1", "0");
                    cmd.Parameters.AddWithValue("@address1", "0");
                    cmd.Parameters.AddWithValue("@postalcode1", DBNull.Value);
                    cmd.Parameters.AddWithValue("@gender1", 0);
                    cmd.Parameters.AddWithValue("@birthday1", DBNull.Value);
                    cmd.Parameters.AddWithValue("@qq1", "0");
                    cmd.Parameters.AddWithValue("@passwd21", hashedPassword);

                    await cmd.ExecuteNonQueryAsync();
                    return "Account created successfully.";
                }
            }
            catch (MySqlException ex)
            {
                return $"Database error (Code: {ex.Number}): {ex.Message} (Verify SP 'adduser' and its parameter names)";
            }
            catch (Exception ex)
            {
                return $"An unexpected error occurred: {ex.Message}";
            }
        }

        private async Task<string> GetDatabaseVerifiedHash(string login, string passwordToVerify)
        {
            string tempLogin = $"{login}_TEMP_USER_CS_{Guid.NewGuid().ToString("N").Substring(0, 5)}";
            string initialHashedPassword = PasswordHelper.PwEncode(login, passwordToVerify);
            string dbVerifiedHash = string.Empty;

            await OpenConnectionAsync();
            MySqlTransaction? transaction = null;
            try
            {
                transaction = await _connection!.BeginTransactionAsync();
                using (var cmdAdd = new MySqlCommand("adduser", _connection, transaction))
                {
                    cmdAdd.CommandType = CommandType.StoredProcedure;
                    cmdAdd.Parameters.AddWithValue("@name1", tempLogin);
                    cmdAdd.Parameters.AddWithValue("@passwd1", initialHashedPassword);
                    cmdAdd.Parameters.AddWithValue("@prompt1", "0");
                    cmdAdd.Parameters.AddWithValue("@answer1", "0");
                    cmdAdd.Parameters.AddWithValue("@truename1", "0");
                    cmdAdd.Parameters.AddWithValue("@idnumber1", "0");
                    cmdAdd.Parameters.AddWithValue("@email1", ""); // Empty email for temp
                    cmdAdd.Parameters.AddWithValue("@mobilenumber1", "0");
                    cmdAdd.Parameters.AddWithValue("@province1", "0");
                    cmdAdd.Parameters.AddWithValue("@city1", "0");
                    cmdAdd.Parameters.AddWithValue("@phonenumber1", "0");
                    cmdAdd.Parameters.AddWithValue("@address1", "0");
                    cmdAdd.Parameters.AddWithValue("@postalcode1", DBNull.Value);
                    cmdAdd.Parameters.AddWithValue("@gender1", 0);
                    cmdAdd.Parameters.AddWithValue("@birthday1", DBNull.Value);
                    cmdAdd.Parameters.AddWithValue("@qq1", "0");
                    cmdAdd.Parameters.AddWithValue("@passwd21", initialHashedPassword);
                    await cmdAdd.ExecuteNonQueryAsync();
                }

                using (var cmdGet = new MySqlCommand("SELECT passwd FROM users WHERE name = @tempLogin_param_select", _connection, transaction))
                {
                    cmdGet.Parameters.AddWithValue("@tempLogin_param_select", tempLogin);
                    var result = await cmdGet.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value) dbVerifiedHash = result.ToString()!; else return string.Empty;
                }

                using (var cmdDel = new MySqlCommand("DELETE FROM users WHERE name = @tempLogin_param_delete", _connection, transaction))
                {
                    cmdDel.Parameters.AddWithValue("@tempLogin_param_delete", tempLogin);
                    await cmdDel.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return dbVerifiedHash;
            }
            catch (MySqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"MySQL Error in GetDatabaseVerifiedHash for {tempLogin}: (Code: {ex.Number}) {ex.Message}");
                if (transaction != null) { try { await transaction.RollbackAsync(); } catch { /* ignore rollback error */ } }
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetDatabaseVerifiedHash for {tempLogin}: {ex.Message}");
                if (transaction != null) { try { await transaction.RollbackAsync(); } catch { /* ignore rollback error */ } }
                return string.Empty;
            }
        }

        [Obsolete("This method should now be called via DaemonGrpcService.")]
        public async Task<string> ChangePasswordAsync(string username, string oldPassword, string newPassword)
        {
            username = username.ToLower();
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
                return "Username, old password, and new password are required.";
            if (newPassword.Length < 4 || newPassword.Length > 10) return "New password must be 4-10 characters.";
            if (!PasswordHelper.IsValidCharacterSet(newPassword)) return "New password contains forbidden characters.";

            try
            {
                await OpenConnectionAsync();
                var userDetails = await GetUserDetailsAsync(username);
                if (userDetails.id == -1) return "User doesn't exist.";

                string currentStoredPassword = userDetails.storedPassword;
                string emailForReuse = userDetails.email;
                int originalUserId = userDetails.id;

                string dbVerifiedOldPasswordHash = await GetDatabaseVerifiedHash(username, oldPassword);

                if (string.IsNullOrEmpty(dbVerifiedOldPasswordHash)) return "Old password verification failed (internal error during hash check).";
                if (dbVerifiedOldPasswordHash != currentStoredPassword) return "Old password mismatch.";

                string newHashedPassword = PasswordHelper.PwEncode(username, newPassword);
                MySqlTransaction? transaction = null;
                try
                {
                    transaction = await _connection!.BeginTransactionAsync();
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "LOCK TABLES users WRITE"; await cmd.ExecuteNonQueryAsync();

                        cmd.CommandText = "DELETE FROM users WHERE ID = @original_id_param_del";
                        cmd.Parameters.AddWithValue("@original_id_param_del", originalUserId);
                        await cmd.ExecuteNonQueryAsync();
                        cmd.Parameters.Clear();

                        cmd.CommandText = "adduser";
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@name1", username);
                        cmd.Parameters.AddWithValue("@passwd1", newHashedPassword);
                        cmd.Parameters.AddWithValue("@prompt1", "0");
                        cmd.Parameters.AddWithValue("@answer1", "0");
                        cmd.Parameters.AddWithValue("@truename1", "0");
                        cmd.Parameters.AddWithValue("@idnumber1", "0");
                        cmd.Parameters.AddWithValue("@email1", emailForReuse);
                        cmd.Parameters.AddWithValue("@mobilenumber1", "0");
                        cmd.Parameters.AddWithValue("@province1", "0");
                        cmd.Parameters.AddWithValue("@city1", "0");
                        cmd.Parameters.AddWithValue("@phonenumber1", "0");
                        cmd.Parameters.AddWithValue("@address1", "0");
                        cmd.Parameters.AddWithValue("@postalcode1", DBNull.Value);
                        cmd.Parameters.AddWithValue("@gender1", 0);
                        cmd.Parameters.AddWithValue("@birthday1", DBNull.Value);
                        cmd.Parameters.AddWithValue("@qq1", "0");
                        cmd.Parameters.AddWithValue("@passwd21", newHashedPassword);
                        await cmd.ExecuteNonQueryAsync();
                        cmd.Parameters.Clear();

                        cmd.CommandText = "UPDATE users SET ID = @original_id_param_update WHERE name = @username_param_update";
                        cmd.CommandType = CommandType.Text;
                        cmd.Parameters.AddWithValue("@original_id_param_update", originalUserId);
                        cmd.Parameters.AddWithValue("@username_param_update", username);
                        await cmd.ExecuteNonQueryAsync();
                        cmd.Parameters.Clear();

                        cmd.CommandText = "UNLOCK TABLES"; await cmd.ExecuteNonQueryAsync();
                    }
                    await transaction.CommitAsync();
                    return "Password changed successfully.";
                }
                catch (MySqlException ex_trans)
                {
                    if (transaction != null) { try { await transaction.RollbackAsync(); } catch { /* ignore rollback error */ } }
                    try { using (var cmdUnlock = _connection!.CreateCommand()) { cmdUnlock.CommandText = "UNLOCK TABLES"; await cmdUnlock.ExecuteNonQueryAsync(); } } catch { /* ignore unlock error */ }
                    System.Diagnostics.Debug.WriteLine($"MySQL Transaction Error in ChangePasswordAsync (Code: {ex_trans.Number}): {ex_trans.Message}");
                    return $"Database transaction error (Code: {ex_trans.Number}): {ex_trans.Message} (Verify SP params for 'adduser')";
                }
                catch (Exception ex_trans)
                {
                    if (transaction != null) { try { await transaction.RollbackAsync(); } catch { /* ignore rollback error */ } }
                    try { using (var cmdUnlock = _connection!.CreateCommand()) { cmdUnlock.CommandText = "UNLOCK TABLES"; await cmdUnlock.ExecuteNonQueryAsync(); } } catch { /* ignore unlock error */ }
                    System.Diagnostics.Debug.WriteLine($"Transaction Error in ChangePasswordAsync: {ex_trans.Message}");
                    return $"Database transaction error: {ex_trans.Message}";
                }
            }
            catch (MySqlException ex)
            {
                return $"Database error (Code: {ex.Number}): {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"An unexpected error occurred: {ex.Message}";
            }
        }

        [Obsolete("This method should now be called via DaemonGrpcService.")]
        public async Task<string> AddCubiAsync(string identifier, bool isById, int amount)
        {
            if (amount < 1 || amount > 999999) return "Invalid Cubi amount (1-999999).";
            int userId = -1;
            try
            {
                await OpenConnectionAsync();
                string query = isById ? "SELECT ID FROM users WHERE ID = @ident_param" : "SELECT ID FROM users WHERE name = @ident_param";
                using (var cmdFind = new MySqlCommand(query, _connection))
                {
                    cmdFind.Parameters.AddWithValue("@ident_param", identifier);
                    var result = await cmdFind.ExecuteScalarAsync();
                    if (result == null || !int.TryParse(result.ToString(), out userId)) return "User doesn't exist.";
                }

                using (var cmd = new MySqlCommand("usecash", _connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@userid1", userId);
                    cmd.Parameters.AddWithValue("@zoneid1", 1);
                    cmd.Parameters.AddWithValue("@sn1", 0);
                    cmd.Parameters.AddWithValue("@aid1", 1);
                    cmd.Parameters.AddWithValue("@point1", 0);
                    cmd.Parameters.AddWithValue("@cash1", amount * 100);
                    cmd.Parameters.AddWithValue("@status1", 1);
                    MySqlParameter errorParam = new MySqlParameter("@error", MySqlDbType.Int32);
                    errorParam.Direction = ParameterDirection.Output;
                    cmd.Parameters.Add(errorParam);
                    await cmd.ExecuteNonQueryAsync();

                    if (errorParam.Value != null && errorParam.Value != DBNull.Value && Convert.ToInt32(errorParam.Value) == 0)
                    {
                        return $"{amount}.00 Cubi added successfully. Relog may be required.";
                    }
                    else if (errorParam.Value != null && errorParam.Value != DBNull.Value)
                    {
                        return $"Cubi operation completed with SP error code: {errorParam.Value}.";
                    }
                    else
                    {
                        return $"{amount}.00 Cubi add command sent. Status unknown (no error code from SP). Relog may be required.";
                    }
                }
            }
            catch (MySqlException ex)
            {
                return $"Database error (Code: {ex.Number}): {ex.Message} (Verify SP 'usecash' parameters)";
            }
            catch (Exception ex)
            {
                return $"An unexpected error occurred: {ex.Message}";
            }
        }

        [Obsolete("This method should now be called via DaemonGrpcService.")]
        public async Task<List<UserAccountInfo>> GetAllUsersAsync()
        {
            var users = new List<UserAccountInfo>();
            var gmUserIds = new HashSet<int>();
            try
            {
                await OpenConnectionAsync();
                using (var cmdGm = new MySqlCommand("SELECT DISTINCT userid FROM auth", _connection))
                using (var readerGm = await cmdGm.ExecuteReaderAsync(CommandBehavior.SingleResult))
                { while (await readerGm.ReadAsync()) { gmUserIds.Add(readerGm.GetInt32("userid")); } }

                using (var cmdUsers = new MySqlCommand("SELECT ID, name, email, creatime FROM users ORDER BY ID", _connection))
                using (var readerUsers = await cmdUsers.ExecuteReaderAsync())
                {
                    while (await readerUsers.ReadAsync())
                    {
                        users.Add(new UserAccountInfo
                        {
                            Id = readerUsers.GetInt32("ID"),
                            Name = readerUsers.GetString("name"),
                            Email = readerUsers.IsDBNull(readerUsers.GetOrdinal("email")) ? "" : readerUsers.GetString("email"),
                            CreateTime = readerUsers.GetDateTime("creatime"),
                            IsGm = gmUserIds.Contains(readerUsers.GetInt32("ID"))
                        });
                    }
                }
                return users;
            }
            catch (MySqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"MySQL Error in GetAllUsersAsync (Code: {ex.Number}): {ex.Message}");
                return users;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAllUsersAsync: {ex.Message}");
                return users;
            }
        }

        [Obsolete("This method should now be called via DaemonGrpcService.")]
        public async Task<string> SetGmStatusAsync(string identifier, bool isById, bool grantAccess)
        {
            int userId = -1;
            try
            {
                await OpenConnectionAsync();
                string query = isById ? "SELECT ID FROM users WHERE ID = @ident_param" : "SELECT ID FROM users WHERE name = @ident_param";
                using (var cmdFind = new MySqlCommand(query, _connection))
                {
                    cmdFind.Parameters.AddWithValue("@ident_param", identifier);
                    var result = await cmdFind.ExecuteScalarAsync();
                    if (result == null || !int.TryParse(result.ToString(), out userId)) return "User doesn't exist.";
                }

                bool currentlyGm = false;
                using (var cmdCheckGm = new MySqlCommand("SELECT COUNT(*) FROM auth WHERE userid = @userId_param", _connection))
                {
                    cmdCheckGm.Parameters.AddWithValue("@userId_param", userId);
                    currentlyGm = Convert.ToInt32(await cmdCheckGm.ExecuteScalarAsync()) > 0;
                }

                if (grantAccess)
                {
                    if (currentlyGm) return "User already has GM access.";
                    using (var cmdSetGm = new MySqlCommand("addGM", _connection))
                    {
                        cmdSetGm.CommandType = CommandType.StoredProcedure;
                        cmdSetGm.Parameters.AddWithValue("@userid", userId);
                        cmdSetGm.Parameters.AddWithValue("@zoneid", 1);
                        await cmdSetGm.ExecuteNonQueryAsync(); return "GM access granted.";
                    }
                }
                else
                {
                    if (!currentlyGm) return "User does not have GM access.";
                    using (var cmdRevokeGm = new MySqlCommand("DELETE FROM auth WHERE userid = @userId_param", _connection))
                    {
                        cmdRevokeGm.Parameters.AddWithValue("@userId_param", userId);
                        int rowsAffected = await cmdRevokeGm.ExecuteNonQueryAsync();
                        return rowsAffected > 0 ? "GM access revoked." : "Failed to revoke GM access (or user was not GM).";
                    }
                }
            }
            catch (MySqlException ex)
            {
                return $"Database error (Code: {ex.Number}): {ex.Message} (Verify SP 'addGM' parameters)";
            }
            catch (Exception ex) { return $"An unexpected error occurred: {ex.Message}"; }
        }

        [Obsolete("This method should now be called via DaemonGrpcService.")]
        public async Task<string> DeleteUserAsync(string identifier, bool isById)
        {
            int userId = -1; string userNameForMessage = identifier;
            try
            {
                await OpenConnectionAsync();
                string query = isById ? "SELECT ID, name FROM users WHERE ID = @ident_param" : "SELECT ID, name FROM users WHERE name = @ident_param";
                using (var cmdFind = new MySqlCommand(query, _connection))
                {
                    cmdFind.Parameters.AddWithValue("@ident_param", identifier);
                    using (var reader = await cmdFind.ExecuteReaderAsync(CommandBehavior.SingleRow))
                    {
                        if (await reader.ReadAsync()) { userId = reader.GetInt32("ID"); userNameForMessage = reader.GetString("name"); }
                        else { return "User doesn't exist."; }
                    }
                }
                using (var cmdDelete = new MySqlCommand("DELETE FROM users WHERE ID = @userId_param", _connection))
                {
                    cmdDelete.Parameters.AddWithValue("@userId_param", userId);
                    int rowsAffected = await cmdDelete.ExecuteNonQueryAsync();
                    return rowsAffected > 0 ? $"Account '{userNameForMessage}' (ID: {userId}) deleted. Check characters." : "Failed to delete.";
                }
            }
            catch (MySqlException ex)
            {
                return $"Database error (Code: {ex.Number}): {ex.Message}";
            }
            catch (Exception ex) { return $"An unexpected error occurred: {ex.Message}"; }
        }

        public async Task<List<PlayerCharacterInfo>> GetCharactersByUserIdAsync(int userId)
        {
            var characters = new List<PlayerCharacterInfo>();
            if (_connection == null && _settings != null) // Ensure _settings is available if _connection is null
            {
                //This indicates OpenConnectionAsync might not have been called or _connection was disposed.
                //Attempt to establish connection here or ensure it's established by caller context.
                //For robust-ness, let's ensure OpenConnectionAsync is callable.
                System.Diagnostics.Debug.WriteLine("Database connection was null in GetCharactersByUserIdAsync. Attempting to open.");
                //await OpenConnectionAsync(); // This line might be problematic if called out of expected flow.
                // It's better if the service ensures connection is managed consistently.
                // For now, rely on prior OpenConnectionAsync or fail gracefully.
                if (_connection == null) // If still null after an attempt or if not attempting here
                {
                    System.Diagnostics.Debug.WriteLine("Database connection not initialized in GetCharactersByUserIdAsync. Returning empty list.");
                    return characters;
                }
            }

            await OpenConnectionAsync(); // Ensure connection is open

            // IMPORTANT: Replace 'roles', 'roleid', 'name', and 'userid'
            // with your actual table and column names for character data.
            const string query = "SELECT roleid, name FROM roles WHERE userid = @userId_param";

            using (var cmd = new MySqlCommand(query, _connection))
            {
                cmd.Parameters.AddWithValue("@userId_param", userId);
                try
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            characters.Add(new PlayerCharacterInfo
                            {
                                RoleId = reader.GetInt32("roleid"),
                                RoleName = reader.IsDBNull(reader.GetOrdinal("name")) ? "Unknown" : reader.GetString("name")
                            });
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MySQL Error in GetCharactersByUserIdAsync for UserID {userId}: (Code: {ex.Number}) {ex.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetCharactersByUserIdAsync for UserID {userId}: {ex.Message}");
                }
            }
            return characters;
        }

        public void Dispose()
        {
            try { CloseConnectionAsync().GetAwaiter().GetResult(); } catch { /* ignore errors on dispose */ }
            _connection?.Dispose();
            _connection = null;
            GC.SuppressFinalize(this);
        }
    }
}