﻿using Microsoft.Extensions.Logging;
using Moq;
using DataModels = NICE.Identity.Authorisation.WebAPI.DataModels;
using ApiModels = NICE.Identity.Authorisation.WebAPI.ApiModels;
using NICE.Identity.Authorisation.WebAPI.Services;
using NICE.Identity.Test.Infrastructure;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NICE.Identity.Authorisation.WebAPI;
using Xunit;
using Xunit.Sdk;

namespace NICE.Identity.Test.UnitTests.Authorisation.WebAPI.Services
{
    public class UserServiceTests : TestBase
    {
        private readonly Mock<ILogger<UsersService>> _logger;
        private readonly Mock<IProviderManagementService> _providerManagementService;

        public UserServiceTests()
        {
            _logger = new Mock<ILogger<UsersService>>();
            _providerManagementService = new Mock<IProviderManagementService>();
        }

        [Fact]
        public void Create_user_where_user_email_exists_but_name_identifier_is_different_and_existing_user_is_migrated_does_not_create_new_user() 
        {
            //Arrange
            const string existingUserEmail = "existing.user@email.com";
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            context.Users.Add(new DataModels.User {NameIdentifier = "some name identifier", EmailAddress = existingUserEmail, IsMigrated = true, IsInAuthenticationProvider = false});
            context.SaveChanges();

            //Act
            userService.CreateUser(new ApiModels.User{NameIdentifier = "another name identifier", EmailAddress = existingUserEmail});

            //Assert
            var allUsers = context.Users.ToList();
            allUsers.Count.ShouldBe(1);
        }

        [Fact]
        public void Create_user_where_user_email_exists_but_name_identifier_is_different_and_existing_user_is_not_migrated() 
        {
	        //Arrange
	        const string existingUserEmail = "existing.user@email.com";
	        var context = GetContext();
	        var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
	        context.Users.Add(new DataModels.User { NameIdentifier = "some name identifier", EmailAddress = existingUserEmail, IsMigrated = false, IsInAuthenticationProvider = true });
	        context.SaveChanges();

	        //Act + Assert
            Assert.Throws<DuplicateEmailException>(() => userService.CreateUser(new ApiModels.User { NameIdentifier = "another name identifier", EmailAddress = existingUserEmail }));
        }

        [Fact]
        public void Create_user_where_user_already_exists()
        {
            //Arrange
            var nameIdentifier = Guid.NewGuid().ToString();

            var context = GetContext();
            context.Users.Add(new DataModels.User { NameIdentifier = nameIdentifier });
            context.SaveChanges();

            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            var userToInsert = new ApiModels.User { NameIdentifier = nameIdentifier };

            //Act + Assert
            userService.CreateUser(userToInsert);
            context.Users.Count().ShouldBe(1);
        }

        [Fact]
        public void Get_single_user_from_userId()
        {
            //Arrange
            const string email = "singleuser@example.com";
            const string nameIdentifier = "user|toget";
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            var user = new ApiModels.User { NameIdentifier = nameIdentifier, EmailAddress = email };
            var createdUserId = userService.CreateUser(user).UserId;

            //Act
            var actual = userService.GetUser(createdUserId.Value);

            //Assert
            actual.EmailAddress.ShouldBe(email);
            actual.NameIdentifier.ShouldBe(nameIdentifier);
        }

        [Fact]
        public void Get_users()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            
            var user1 = new ApiModels.User { NameIdentifier = "auth|user1", EmailAddress = "user1@example.com" };
            var user2 = new ApiModels.User { NameIdentifier = "auth|user2", EmailAddress = "user2@example.com" };
            var createdUser1 = userService.CreateUser(user1);
            var createdUser2 = userService.CreateUser(user2);

            //Act
            var users = userService.GetUsers();

            //Assert
            context.Users.Count().ShouldBe(2);
            users.Count().ShouldBe(2);
            users.First(u => u.UserId == createdUser1.UserId).NameIdentifier.ShouldBe("auth|user1");
            users.First(u => u.UserId == createdUser2.UserId).NameIdentifier.ShouldBe("auth|user2");
        }
        
        [Fact]
        public void Get_users_with_filter()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            
            userService.CreateUser(new ApiModels.User 
            {
                NameIdentifier = "auth|user2",
                FirstName = "FirstName2",
                LastName = "LastName2",
                EmailAddress = "user2@example.com"
            });
            userService.CreateUser(new ApiModels.User
            {
	            NameIdentifier = "auth|user1",
	            FirstName = "FirstName1",
	            LastName = "LastName1",
	            EmailAddress = "user1@example.com"
            });
            context.SaveChanges();

            //Act
            var usersFilterByFirstName = userService.GetUsers("FirstName1");
            var usersFilterByLastName = userService.GetUsers("LastName2");
            var usersFilterByEmailAddress = userService.GetUsers("user1@example.com");
            var usersFilterByNameIdentifier = userService.GetUsers("auth|user1");
            var usersFilterMultiple = userService.GetUsers("example.com");
            var usersFilterWithFirstNameAndLastName = userService.GetUsers("Firstname1 Lastname1");

            //Assert
            context.Users.Count().ShouldBe(2);
            
            //note: users are returned sorted by most recently added first

            usersFilterByFirstName.Count.ShouldBe(1);
            usersFilterByFirstName.First().NameIdentifier.ShouldBe("auth|user1");

            usersFilterByLastName.Count.ShouldBe(1);
            usersFilterByLastName.First().NameIdentifier.ShouldBe("auth|user2");

            usersFilterByEmailAddress.Count.ShouldBe(1);
            usersFilterByEmailAddress.First().NameIdentifier.ShouldBe("auth|user1");
            
            usersFilterByNameIdentifier.Count.ShouldBe(1);
            usersFilterByNameIdentifier.First().NameIdentifier.ShouldBe("auth|user1");

            usersFilterMultiple.Count.ShouldBe(2);
            usersFilterMultiple.First().NameIdentifier.ShouldBe("auth|user1");
            usersFilterMultiple.Last().NameIdentifier.ShouldBe("auth|user2");

            usersFilterWithFirstNameAndLastName.Count.ShouldBe(1);
            usersFilterWithFirstNameAndLastName.Single().NameIdentifier.ShouldBe("auth|user1");
        }

        [Fact]
        public void Get_users_with_filter_not_found()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            
            userService.CreateUser(new ApiModels.User
            {
                NameIdentifier = "auth|user1",
                FirstName = "FirstName1",
                LastName = "LastName1",
                EmailAddress = "user1@example.com"
            });
            userService.CreateUser(new ApiModels.User
            {
                NameIdentifier = "auth|user2",
                FirstName = "FirstName2",
                LastName = "LastName2",
                EmailAddress = "user2@example.com"
            });
            context.SaveChanges();

            //Act
            var usersFilterNotFound = userService.GetUsers("Name3");

            //Assert
            context.Users.Count().ShouldBe(2);
            usersFilterNotFound.Count().ShouldBe(0);
            usersFilterNotFound.ShouldNotBe(null);
        }

        [Fact]
        public async Task Update_user_that_already_exists()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            var user = new ApiModels.User(){ EmailAddress = "existing.user@email.com", FirstName = "Joe"};
            var createdUserId = userService.CreateUser(user).UserId;

            //Act
            var userToUpdate = userService.GetUser(createdUserId.Value);
            userToUpdate.FirstName = "John";
            var updatedUser = await userService.UpdateUser(createdUserId.Value, userToUpdate);

            //Assert
            context.Users.ToList().Count.ShouldBe(1);
            updatedUser.FirstName.ShouldBe("John");
        }

        [Fact]
        public void Update_user_that_does_not_exist()
        {
            //Arrange
            const int nonExistingUserId = 987;
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            var userToUpdate = new ApiModels.User()
            {
                UserId = nonExistingUserId, 
                EmailAddress = "random.user@email.com", 
                FirstName = "Joe"
            };

            //Act + Assert
            Assert.ThrowsAsync<NullReferenceException>(async () => await userService.UpdateUser(nonExistingUserId, userToUpdate));
            context.Users.Count().ShouldBe(0);
        }

        [Fact]
        public async Task Delete_single_user_with_userId()
        {
            //Arrange
            const string newUserEmailAddress = "usertodelete@example.com";
            const string nameIdentifier = "user|todelete";
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            var user = new ApiModels.User { NameIdentifier = nameIdentifier, EmailAddress = newUserEmailAddress };
            var createdUser = userService.CreateUser(user);

            //Act
            var deleteReturn = await userService.DeleteUser(createdUser.UserId.Value);

            //Assert
            var deletedUser = userService.GetUser(createdUser.UserId.Value);
            deletedUser.ShouldBe(null);
            deleteReturn.ShouldBe(1);
        }
        
        [Fact]
        public void Get_roles_for_user_by_website()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            TestData.AddEnvironment(ref context, 1);
            TestData.AddService(ref context, 1);
            TestData.AddWebsite(ref context, 1, 1, 1);
            TestData.AddRole(ref context, 1, 1, "TestRole");
            TestData.AddUser(ref context, 1);
            TestData.AddUserRole(ref context);
            context.SaveChanges();

            //Act
            var userRolesByWebsite =  userService.GetRolesForUserByWebsite(1, 1);

            //Assert
            userRolesByWebsite.Roles.Count().ShouldBe(1);
            userRolesByWebsite.Roles.First().Name.ShouldBe("TestRole");
            userRolesByWebsite.UserId.ShouldBe(1);
            userRolesByWebsite.WebsiteId.ShouldBe(1);
        }

        [Fact]
        public void Update_roles_for_user_by_website()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            TestData.AddEnvironment(ref context, 1);
            TestData.AddService(ref context, 1);
            TestData.AddWebsite(ref context, 1, 1, 1);
            TestData.AddRole(ref context, 1, 1, "TestRole1");
            TestData.AddRole(ref context, 2, 1, "TestRole2");
            TestData.AddRole(ref context, 3, 1, "TestRole3");
            TestData.AddUser(ref context, 1);
            context.UserRoles.Add(new DataModels.UserRole() {UserId = 1, RoleId = 1});
            context.UserRoles.Add(new DataModels.UserRole() {UserId = 1, RoleId = 2});
            context.SaveChanges();

            //Act
            userService.UpdateRolesForUserByWebsite(
                1,1, new ApiModels.UserRolesByWebsite()
                {
                    UserId = 1,
                    WebsiteId = 1,
                    Roles = new List<ApiModels.UserRoleDetailed>()
                    {
                        new ApiModels.UserRoleDetailed()
                        {
                            Id = 1,
                            HasRole = true
                        },
                        new ApiModels.UserRoleDetailed()
                        {
                            Id = 2,
                            HasRole = false
                        },
                        new ApiModels.UserRoleDetailed()
                        {
                            Id = 3,
                            HasRole = true
                        }
                    }
                });

            //Assert
            var userRolesByWebsite =  userService.GetRolesForUserByWebsite(1, 1);
            userRolesByWebsite.UserId.ShouldBe(1);
            userRolesByWebsite.WebsiteId.ShouldBe(1);
            userRolesByWebsite.Roles.Find(r => r.Id == 1).HasRole.ShouldBe(true);
            userRolesByWebsite.Roles.Find(r => r.Id == 2).HasRole.ShouldBe(false);
            userRolesByWebsite.Roles.Find(r => r.Id == 3).HasRole.ShouldBe(true);
        }

        [Fact]
        public void Get_roles_for_user()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            TestData.AddEnvironment(ref context, 1);
            TestData.AddService(ref context, 1);
            TestData.AddWebsite(ref context, 1, 1, 1);
            TestData.AddRole(ref context, 1, 1, "TestRole1");
            TestData.AddRole(ref context, 2, 1, "TestRole2");
            TestData.AddUser(ref context, 1);
            TestData.AddUserRole(ref context, 1, 1, 1);
            TestData.AddUserRole(ref context, 2, 2, 1);
            context.SaveChanges();

            //Act
            var userRoles =  userService.GetRolesForUser(1);

            //Assert
            userRoles.Count().ShouldBe(2);
            userRoles.First(r=>r.UserRoleId == 1).RoleId.ShouldBe(1);
            userRoles.First(r=>r.UserRoleId == 2).RoleId.ShouldBe(2);
        }
        
        [Fact]
        public void Update_roles_for_user()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            context.Environments.Add(new DataModels.Environment() { EnvironmentId = 1});
            context.Services.Add(new DataModels.Service(){ServiceId = 1});
            context.Websites.Add(new DataModels.Website(){WebsiteId = 1, ServiceId = 1, EnvironmentId = 1});
            context.Roles.Add(new DataModels.Role() { RoleId = 1, Name = "TestRole1"});
            context.Roles.Add(new DataModels.Role() { RoleId = 2, Name = "TestRole2"});
            context.Users.Add(new DataModels.User(){UserId = 1});
            context.SaveChanges();

            //Act
            var userRoles =  userService.UpdateRolesForUser(1, new List<ApiModels.UserRole>()
            {
                new ApiModels.UserRole(){UserId = 1, RoleId = 1},
                new ApiModels.UserRole(){UserId = 1, RoleId = 2}
            });

            //Assert
            userRoles.Count().ShouldBe(2);
            userRoles.SingleOrDefault(ur => ur.UserId == 1 && ur.RoleId == 1).ShouldNotBeNull();
            userRoles.SingleOrDefault(ur => ur.UserId == 1 && ur.RoleId == 2).ShouldNotBeNull();
        }

        [Fact]
        public void Find_users()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            context.Environments.Add(new DataModels.Environment() { EnvironmentId = 1, Name = "Dev"});
            context.Environments.Add(new DataModels.Environment() { EnvironmentId = 2, Name = "Test"});
            context.Services.Add(new DataModels.Service(){ServiceId = 1, Name = "Service"});
            context.Websites.Add(new DataModels.Website()
            {
                WebsiteId = 1, EnvironmentId = 1, ServiceId = 1, Host = "dev.nice.org.uk"
            });
            context.Roles.Add(new DataModels.Role() {RoleId = 1, WebsiteId = 1, Name = "TestRole1"});
            context.Roles.Add(new DataModels.Role() {RoleId = 2, WebsiteId = 1, Name = "TestRole2"});
            context.Users.Add(new DataModels.User(){UserId = 1, NameIdentifier = "auth|user1"});
            context.Users.Add(new DataModels.User(){UserId = 2, NameIdentifier = "auth|user2"});
            context.SaveChanges();

            //Act
            var users = userService.FindUsers(new []{"auth|user1","auth|user2"});

            //Assert
            users.Count().ShouldBe(2);
            users[0].NameIdentifier.ShouldBe("auth|user1");
            users[1].NameIdentifier.ShouldBe("auth|user2");
        }

        [Fact]
        public void Find_roles()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            context.Environments.Add(new DataModels.Environment() { EnvironmentId = 1, Name = "Test"});
            context.Services.Add(new DataModels.Service(){ServiceId = 1, Name = "Service"});
            context.Websites.Add(new DataModels.Website()
            {
                WebsiteId = 1, EnvironmentId = 1, ServiceId = 1, Host = "test.nice.org.uk"
            });
            context.Roles.Add(new DataModels.Role() {RoleId = 1, WebsiteId = 1, Name = "TestRole"});
            context.Users.Add(new DataModels.User(){UserId = 1, NameIdentifier = "auth|user1"});
            context.UserRoles.Add(new DataModels.UserRole() { UserRoleId = 1, RoleId = 1, UserId = 1});
            context.SaveChanges();

            //Act
            var users = userService.FindRoles(new []{"auth|user1","auth|user2"}, "test.nice.org.uk");

            //Assert
            users["auth|user1"].First().ShouldBe("TestRole");
        }
        
        [Fact]
        public void Import_users()
        {
            //Arrange
            var context = GetContext();
            var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
            context.Environments.Add(new DataModels.Environment() { EnvironmentId = 1, Name = "Test"});
            context.Services.Add(new DataModels.Service(){ServiceId = 1, Name = "Service"});
            context.Websites.Add(new DataModels.Website()
            {
                WebsiteId = 1, EnvironmentId = 1, ServiceId = 1, Host = "test.nice.org.uk"
            });
            context.Roles.Add(new DataModels.Role() {RoleId = 1, WebsiteId = 1, Name = "TestRole1"});
            context.SaveChanges();

            //Act
            userService.ImportUsers(new List<DataModels.ImportUser>()
            {
                new DataModels.ImportUser()
                {
                    UserId = Guid.NewGuid().ToString(),
                    FirstName = "FirstName",
                    LastName = "LastName",
                    EmailAddress = "FirstName.LastName@nice.org.uk",
                    Roles = new List<DataModels.ImportRole>()
                    {
                        new DataModels.ImportRole(){RoleName = "TestRole1", WebsiteHost = "test.nice.org.uk"},
                    }
                },
            });

            //Assert
            context.Users.Count().ShouldBe(1);
            var user = context.Users.First();
            user.FirstName.ShouldBe("FirstName");
            context.UserRoles.First(ur => ur.User.UserId == user.UserId).Role.Name.ShouldBe("TestRole1");
        }

        [Fact]
        public void Import_users_when_existing_user_available_does_not_create_duplicate()
        {
	        //Arrange
	        var context = GetContext();
	        var existingEmailAddress = "existing.user.account@nice.org.uk";
	        var existingFirstName = "Phil";
	        var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
	        context.Users.Add(new DataModels.User() { EmailAddress = existingEmailAddress, FirstName = existingFirstName, LastName = "Connors"});
            context.SaveChanges();

	        //Act
	        userService.ImportUsers(new List<DataModels.ImportUser>()
	        {
		        new DataModels.ImportUser()
		        {
			        UserId = Guid.NewGuid().ToString(),
			        FirstName = "Same",
			        LastName = "User",
			        EmailAddress = existingEmailAddress
		        },
	        });

	        //Assert
	        context.Users.Count().ShouldBe(1);
	        var user = context.Users.First();
	        user.FirstName.ShouldBe(existingFirstName);
        }

        [Fact]
        public void Import_users_when_existing_user_available_does_not_create_duplicate_and_sets_roles_correctly()
        {
	        //Arrange
	        var context = GetContext();
	        var existingEmailAddress = "existing.user.account@nice.org.uk";
	        var existingFirstName = "Phil";
	        var userService = new UsersService(context, _logger.Object, _providerManagementService.Object);
	        context.Users.Add(new DataModels.User() { EmailAddress = existingEmailAddress, FirstName = existingFirstName, LastName = "Connors" });
	        context.Environments.Add(new DataModels.Environment() { EnvironmentId = 1, Name = "Test" });
	        context.Services.Add(new DataModels.Service() { ServiceId = 1, Name = "Service" });
	        context.Websites.Add(new DataModels.Website()
	        {
		        WebsiteId = 1,
		        EnvironmentId = 1,
		        ServiceId = 1,
		        Host = "test.nice.org.uk"
	        });
	        context.Roles.Add(new DataModels.Role() { RoleId = 1, WebsiteId = 1, Name = "TestRole1" });
            context.SaveChanges();

	        //Act
	        userService.ImportUsers(new List<DataModels.ImportUser>()
	        {
		        new DataModels.ImportUser()
		        {
			        UserId = Guid.NewGuid().ToString(),
			        FirstName = "Same",
			        LastName = "User",
			        EmailAddress = existingEmailAddress,
			        Roles = new List<DataModels.ImportRole>()
			        {
				        new DataModels.ImportRole(){RoleName = "TestRole1", WebsiteHost = "test.nice.org.uk"},
			        }
                },
	        });

	        //Assert
	        context.Users.Count().ShouldBe(1);
	        var user = context.Users.First();
	        user.FirstName.ShouldBe(existingFirstName);
	        context.UserRoles.First(ur => ur.User.UserId == user.UserId).Role.Name.ShouldBe("TestRole1");
        }
    }
}
