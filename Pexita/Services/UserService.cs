﻿using AutoMapper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pexita.Data;
using Pexita.Data.Entities.Authentication;
using Pexita.Data.Entities.Comments;
using Pexita.Data.Entities.User;
using Pexita.Services.Interfaces;
using Pexita.Utility.Exceptions;
using System.Text.RegularExpressions;

namespace Pexita.Services
{
    public class UserService : IUserService
    {
        private readonly AppDBContext _Context;
        private readonly IPexitaTools _pexitaTools;
        private readonly IMapper _mapper;
        private readonly JwtSettings _jwtSettings;
        private readonly IEmailService _emailService;
        public UserService(AppDBContext Context, IPexitaTools PexitaTools, IMapper Mapper, JwtSettings jwtSettings, IEmailService emailService)
        {
            _Context = Context;
            _pexitaTools = PexitaTools;
            _mapper = Mapper;
            _jwtSettings = jwtSettings;
            _emailService = emailService;
        }

        /// <summary>
        /// Gets the list of all users with respective orders and addresses.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public List<UserInfoVM> GetUsers()
        {
            var users = _Context.Users
                .Include(u => u.Orders)
                .Include(u => u.Addresses)
                .AsNoTracking()
                .ToList();
            if (users.Count == 0)
            {
                throw new NotFoundException("No User Found");
            }
            return users.Select(UserModelToInfoVM).ToList();
        }
        /// <summary>
        /// gets a certain number of users from database along with their orders and addresses. 
        /// </summary>
        /// <param name="Count"></param>
        /// <returns>a list of UserinfoVM object containing accessible data from that user.</returns>
        /// <exception cref="NotFoundException"></exception>
        public List<UserInfoVM> GetUsers(int Count)
        {
            var users = _Context.Users
                .Include(u => u.Orders)
                .Include(u => u.Addresses)
                .AsNoTracking()
                .Take(Count)
                .ToList();

            if (users.Count == 0)
            {
                throw new NotFoundException("No User Found");
            }

            return users.Select(UserModelToInfoVM).ToList();
        }
        /// <summary>
        /// Gets a certain user's info using their ID.
        /// AS NO TRACKING.
        /// </summary>
        /// <param name="UserID">ID of the user you want to access.</param>
        /// <returns>a UserinfoVM object containing accessible data from that user.</returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<UserInfoVM> GetUserByID(int UserID)
        {
            UserModel user = await _Context.Users
                .Include(u => u.Orders).Include(u => u.Addresses)
                .Include(u => u.BrandNewsletters).Include(u => u.ProductNewsletters)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ID == UserID) ?? throw new NotFoundException($"User With ID:{UserID} Not Found");

            return UserModelToInfoVM(user);
        }
        /// <summary>
        /// finding a user By using user's Username.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns>returns a <see cref="UserInfoVM"/> object containing accessible info.</returns>
        /// <exception cref="NotFoundException"></exception>
        public UserInfoVM GetUserByUserName(string userName)
        {
            UserModel user = _Context.Users
                .Include(u => u.Orders).Include(u => u.Addresses)
                .Include(u => u.BrandNewsletters)
                .Include(u => u.ProductNewsletters)
                .AsNoTracking()
                .FirstOrDefault(u => u.Username == userName)
                ?? throw new NotFoundException($"User With Username:{userName} Not Found");

            return UserModelToInfoVM(user);
        }
        /// <summary>
        /// changes a user's password after making sure they're valid.
        /// </summary>
        /// <param name="userID">the user whose password is going to change.</param>
        /// <param name="NewPassword"></param>
        /// <param name="ConfirmPassword"></param>
        /// <param name="requestingUsername">the user requesting the change.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public async Task<UserInfoVM> ChangePassword(UserInfoVM userInfo, string NewPassword, string ConfirmPassword, string requestingUsername)
        {
            if (NewPassword.IsNullOrEmpty() || ConfirmPassword.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(NewPassword));
            else if (NewPassword != ConfirmPassword)
                throw new ArgumentException($"Entered values {NewPassword} and {ConfirmPassword} Do not match.");

            // checking if the user has the authorization to access this.
            UserModel user = await _pexitaTools.AuthorizeUserAccessAsync(userInfo.ID, requestingUsername);
            string hashedpassword = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            user.Password = hashedpassword;
            user.ResetPasswordCode = string.Empty;
            await _Context.SaveChangesAsync();
            return UserModelToInfoVM(user, userInfo.RefreshToken!, userInfo.JWToken!);
        }
        /// <summary>
        /// begins a Change password procedure for the user.
        /// </summary>
        /// <param name="userinfo">the user whom wants to change their password.</param>
        /// <returns>a <see cref="UserInfoVM"/> object containing Info. </returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task<UserInfoVM> ResetPassword(string userinfo)
        {
            if (userinfo.IsNullOrEmpty())
                throw new ArgumentNullException(userinfo);

            string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            UserModel? user;

            if (Regex.IsMatch(userinfo, emailPattern)) // if the user has entered an email:
                user = await _Context.Users.FirstOrDefaultAsync(u => u.Email == userinfo); // we search by email
            else // if it's not an email then the user has entered their username
                user = await _Context.Users.FirstOrDefaultAsync(user => user.Username == userinfo); // we search by username

            if (user == null) // if no user exists with that email/username:
                throw new NotFoundException($"User {userinfo} does not exist.");

            user.ResetPasswordCode = _pexitaTools.GenerateRandomPassword(8); // we generate a reset password code for them,
            string Subject = "Pexita Authentication code";
            string Body = $"Your Authentication Code Is {user.ResetPasswordCode}";
            _emailService.SendEmail(user.Email, Subject, Body); // we send the code to the user.

            await _Context.SaveChangesAsync();

            return UserModelToInfoVM(user);
        }
        /// <summary>
        /// checks if the given code matches the one in Database.
        /// </summary>
        /// <param name="userID">user whom we want to reset.</param>
        /// <param name="Code">the ResetCode</param>
        /// <returns>True if match, false otherwise.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task<UserInfoVM> CheckResetCode(UserInfoVM user, string Code)
        {
            if (Code.IsNullOrEmpty())
                throw new ArgumentNullException(Code);

            UserModel userRec = await _Context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ID == user.ID)
                ?? throw new NotFoundException($"User {user.ID} does not exist.");

            string ResetCode = userRec.ResetPasswordCode ?? throw new ArgumentNullException("ResetCode");

            if (ResetCode == Code)
            {
                var result = UserModelToInfoVM(userRec);
                string token = _pexitaTools.GenerateJWToken(userRec.Username, userRec.Role, userRec.Email);
                string refToken = _pexitaTools.GenerateRefreshToken();
                RefreshToken refreshToken = new()
                {
                    Token = token,
                    User = userRec,
                    UserId = userRec.ID,
                    Created = DateTime.UtcNow,
                    Expires = DateTime.UtcNow.AddDays(7)
                };
                _Context.RefreshTokens.Add(refreshToken);
                await _Context.SaveChangesAsync();

                result.RefreshToken = refreshToken;
                result.JWToken = token;
                return result;
            }
            throw new ArgumentException("Code is Wrong.");

        } 
        /// <summary>
        /// updates a user's cred in database.
        /// </summary>
        /// <param name="userUpdateVM">new information and changes.</param>
        /// <param name="requestingUsername">the username requesting the change.</param>
        /// <returns>a <see cref="UserInfoVM"/> object containing new record's info.</returns>
        /// <exception cref="ValidationException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task<UserInfoVM> UpdateUser(UserUpdateVM userUpdateVM, string requestingUsername)
        {
            try
            {
                UserModel User = await _pexitaTools.AuthorizeUserAccessAsync(userUpdateVM.ID, requestingUsername);

                _mapper.Map(userUpdateVM, User);

                await _Context.SaveChangesAsync();

                return UserModelToInfoVM(User);
            }

            catch (ValidationException e)
            {
                throw new ValidationException(e.Message);
            }

            catch (InvalidOperationException)
            {
                throw new NotFoundException($"User With ID:{userUpdateVM.ID} Not Found");
            }
        }
        /// <summary>
        /// Deletes a user account from database.
        /// </summary>
        /// <param name="UserID">ID of the user to be deleted.</param>
        /// <param name="requestingUsername">username of the user requesting the deletion.</param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task DeleteUser(int UserID, string requestingUsername)
        {
            UserModel user = await _pexitaTools.AuthorizeUserAccessAsync(UserID, requestingUsername);
            _Context.Remove(user);
            await _Context.SaveChangesAsync();
        }
        /// <summary>
        /// Maps a UserModel database record to a representable object.
        /// </summary>
        /// <param name="userModel">the database record.</param>
        /// <returns>a <see cref="UserInfoVM"/> object containing information.</returns>
        public UserInfoVM UserModelToInfoVM(UserModel userModel)
        {
            return _mapper.Map<UserInfoVM>(userModel);
        }
        /// <summary>
        /// Maps a UserModel database record to a representable object.
        /// </summary>
        /// <param name="userModel">the database record.</param>
        /// <param name="RefreshToken">RefreshToken of the user.</param>
        /// <param name="AccessToken">JWToken given to user to authenticate their requests.</param>
        /// <returns>a <see cref="UserInfoVM"/> object containing information.</returns>
        public UserInfoVM UserModelToInfoVM(UserModel userModel, RefreshToken RefreshToken, string AccessToken)
        {
            var result = _mapper.Map<UserInfoVM>(userModel);
            result.RefreshToken = RefreshToken;
            result.JWToken = AccessToken;
            return result;
        }
        /// <summary>
        /// logs a user in and gives them respective tokens to surf across webpages.
        /// </summary>
        /// <param name="userLoginVM">user login info</param>
        /// <returns>a <see cref="UserInfoVM"/> object containing information.</returns>
        /// <exception cref="NotFoundException"></exception>
        /// <exception cref="NotAuthorizedException"></exception>
        public async Task<UserInfoVM> Login(UserLoginVM userLoginVM)
        {
            // TODO: Implement token service and refresh token for Brands too.
            UserModel? user = null;

            if (!string.IsNullOrEmpty(userLoginVM.UserName))
                user = await _Context.Users.FirstOrDefaultAsync(u => u.Username == userLoginVM.UserName) ?? throw new NotFoundException();

            else if (string.IsNullOrEmpty(userLoginVM.Email))
                user = await _Context.Users.FirstOrDefaultAsync(u => u.Email == userLoginVM.Email) ?? throw new NotFoundException();

            if (user == null && !BCrypt.Net.BCrypt.Verify(userLoginVM.Password, user?.Password))
            {
                throw new NotAuthorizedException("Username or Password is not correct");
            }

            var result = UserModelToInfoVM(user);
            result.JWToken = _pexitaTools.GenerateJWToken(user.Username, user.Role, user.Email);
            string rawRefreshToken = _pexitaTools.GenerateRefreshToken();
            RefreshToken refreshToken = new RefreshToken()
            {
                Token = rawRefreshToken,
                User = user,
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
                UserId = user.ID
            };
            _Context.RefreshTokens.Add(refreshToken);
            result.RefreshToken = refreshToken;
            return result;
        }
        /// <summary>
        /// Generates a fresh JWToken for the user given the refreshToken.
        /// </summary>
        /// <param name="refreshToken">the string containing user's given refreshToken.</param>
        /// <returns>a fresh JWToken.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task<UserInfoVM> TokenRefresher(string refreshToken)
        {
            if (refreshToken.IsNullOrEmpty())
                throw new ArgumentNullException(refreshToken);

            RefreshToken? currentRefreshToken = await _Context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
            if (currentRefreshToken == null || !currentRefreshToken.IsActive)
                throw new NotFoundException($"token {refreshToken} is not valid.");
            UserModel user = await _Context.Users.FindAsync(currentRefreshToken.UserId) ?? throw new NotFoundException($"User {currentRefreshToken.UserId} Does not exist");

            var result = UserModelToInfoVM(user);
            // Generating both new JWToken and RefreshToken
            var newRefreshTokenStr = _pexitaTools.GenerateRefreshToken();
            result.JWToken = _pexitaTools.GenerateJWToken(user.Username, user.Role, user.Email);

            // Revoking the current token that the user had.
            currentRefreshToken.Revoked = DateTime.UtcNow;
            _Context.RefreshTokens.Update(currentRefreshToken);
            // Creating the new Refresh token object for the user.
            var newToken = new RefreshToken()
            {
                Token = newRefreshTokenStr,
                User = user,
                UserId = user.ID,
                Created = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            _Context.RefreshTokens.Add(newToken);
            result.RefreshToken = newToken;

            await _Context.SaveChangesAsync();
            return result;
        }
        /// <summary>
        /// revokes a user's refresh token on their logout
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task RevokeToken(string token)
        {
            if (token == null) throw new ArgumentNullException(token);
            var tokenRecord = await _Context.RefreshTokens.FirstOrDefaultAsync(t => t.Token == token) ?? throw new NotFoundException();
            if (tokenRecord != null && tokenRecord.IsActive)
            {
                tokenRecord.Revoked = DateTime.UtcNow;
                _Context.RefreshTokens.Update(tokenRecord);
                await _Context.SaveChangesAsync();
            }
        }
        /// <summary>
        /// registers a new user.
        /// </summary>
        /// <param name="userCreateVM">object containing information about the new user.</param>
        /// <returns>a <see cref="UserInfoVM"/> object containing information about the new user.</returns>
        public async Task<UserInfoVM> Register(UserCreateVM userCreateVM)
        {
            if (await _Context.Users.AnyAsync(u => u.Username == userCreateVM.Username))
                throw new ArgumentException("User already exists.");

            UserModel User = _mapper.Map<UserModel>(userCreateVM);
            User.Password = BCrypt.Net.BCrypt.HashPassword(User.Password);
            _Context.Users.Add(User);

            await _Context.SaveChangesAsync();

            return UserModelToInfoVM(User);

        }
        // TODO: make sure all user controllers have unAuthorized catch
        /// <summary>
        /// Gets all of the addresses of a given user.
        /// </summary>
        /// <param name="UserID">ID of the user.</param>
        /// <param name="requestingUsername">Username of the user requesting.</param>
        /// <returns>a list of all addresses of a given user.</returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<List<Address>> GetAddresses(int UserID, string requestingUsername)
        {
            UserModel user = await _Context.Users.Include(u => u.Addresses).FirstOrDefaultAsync(u => u.ID == UserID) ?? throw new NotFoundException($"User With ID:{UserID} Not Found");
            return user.Addresses!.ToList();
        }
        /// <summary>
        /// Adds an address to the collection of a user.
        /// </summary>
        /// <param name="UserID"></param>
        /// <param name="address"></param>
        /// <param name="requestingUsername"></param>
        /// <returns></returns>
        /// <exception cref="NotAuthorizedException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task AddAddress(int UserID, Address address, string requestingUsername)
        {

            UserModel user = await _pexitaTools.AuthorizeUserAccessAsync(UserID, requestingUsername);

            if (user.Addresses == null)
                user.Addresses = new List<Address>();

            user.Addresses.Add(address);

            await _Context.SaveChangesAsync();
        }
        /// <summary>
        /// updates a given address in database.
        /// </summary>
        /// <param name="UserID"></param>
        /// <param name="address"></param>
        /// <param name="requestingUsername"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task UpdateAddress(int UserID, Address address, string requestingUsername)
        {
            UserModel user = await _pexitaTools.AuthorizeUserAccessAsync(UserID, requestingUsername);

            // Find the existing address in the user's addresses list
            Address existingAddress = user.Addresses!.FirstOrDefault(a => a.ID == address.ID) ?? throw new NotFoundException($"Address With ID:{address.ID} Not Found");

            // Update the properties of the existing address with the values from the 'address' parameter
            existingAddress.Province = address.Province;
            existingAddress.City = address.City;
            existingAddress.Text = address.Text;

            await _Context.SaveChangesAsync();
        }
        /// <summary>
        /// Deletes an address from database.
        /// </summary>
        /// <param name="UserID"></param>
        /// <param name="id"></param>
        /// <param name="requestingUsername"></param>
        /// <returns></returns>
        /// <exception cref="NotAuthorizedException"></exception>
        /// <exception cref="NotFoundException"></exception>
        public async Task DeleteAddress(int UserID, int id, string requestingUsername)
        {
            UserModel user = await _pexitaTools.AuthorizeUserAccessAsync(UserID, requestingUsername);

            _Context.Remove(user.Addresses!.FirstOrDefault(a => a.ID == id) ?? throw new NotFoundException($"Address with ID {id} Not found"));

            await _Context.SaveChangesAsync();
        }
        /// <summary>
        /// gets all comments of a user.
        /// </summary>
        /// <param name="UserID"></param>
        /// <returns>a list containing all comments of a given user.</returns>
        /// <exception cref="NotFoundException"></exception>
        public async Task<List<CommentsModel>> GetComments(int UserID)
        {
            UserModel user = await _Context.Users.Include(u => u.Comments).FirstOrDefaultAsync(u => u.ID == UserID) ?? throw new NotFoundException($"User With ID:{UserID} Not Found");
            return user.Comments!.ToList();
        }
        /// <summary>
        /// checks if a given id is a valid user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsUser(int id)
        {
            return _Context.Users.FirstOrDefault(u => u.ID == id) != null;
        }
        /// <summary>
        /// checks if a given username is a valid user.
        /// </summary>
        /// <param name="Username"></param>
        /// <returns></returns>
        public bool IsUser(string Username)
        {
            return _Context.Users.FirstOrDefault(u => u.Username == Username) != null;
        }
        /// <summary>
        /// checks if a given email is already used.
        /// </summary>
        /// <param name="Email"></param>
        /// <returns>True if in use, false otherwise.</returns>
        public bool IsEmailInUse(string Email)
        {
            return _Context.Users.FirstOrDefault(_u => _u.Email == Email) != null;
        }
        /// <summary>
        /// checks whether a given user is an admin.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns>True if is admin, false otherwise.</returns>
        /// <exception cref="NotFoundException"></exception>
        public bool IsAdmin(string userName)
        {
            var user = _Context.Users.FirstOrDefault(u => u.Username == userName) ?? throw new NotFoundException();
            return user.Role == "admin";
        }
    }
}
