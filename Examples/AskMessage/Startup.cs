using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqrlForNet;

namespace AskMessageExample
{
	public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
                .AddCookie(options =>
                {
                    options.LoginPath = "/SignIn";
                    options.LogoutPath = "/SignOut";
                    options.ExpireTimeSpan = TimeSpan.FromHours(3);
                })
                .AddSqrl(options =>
                {
                    options.CheckMilliSeconds = 1000;
                    options.CreateUser = SqrlCreateUser;
                    options.UserExists = UserExists;
                    options.UpdateUserId = UpdateUserId;
                    options.RemoveUser = RemoveUser;
                    options.LockUser = LockUser;
                    options.UnlockUser = UnlockUser;
                    options.GetUserVuk = GetUserVuk;
                    options.GetUserSuk = GetUserSuk;
                    options.Events.OnTicketReceived += OnTicketReceived;
                    options.Diagnostics = true;
                    options.DisableDefaultLoginPage = true;
                    options.EnableHelpers = true;
                    options.HelpersPaths = new[]
                    {
                        new PathString("/"),
                        new PathString("/MessageMe")
                    };
                    options.OtherAuthenticationPaths = new OtherAuthenticationPath[]
                    {
                        new OtherAuthenticationPath()
                        {
                            Path = "/MessageMe/Now",
                            AuthenticateSeparately = false
                        }
                    };
                    options.GetAskQuestion = GetAskQuestion;
                    options.ProcessAskResponse = ProcessAskResponse;
                });

            services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
            });
        }

        private AskMessage GetAskQuestion(HttpRequest request, string sendingNut)
        {
            if (request.Path.StartsWithSegments("/MessageMe/Now"))
            {
                return new AskMessage()
                {
                    Message = "This is a demo message for the SQRL client to show. Did you see the message?",
                    Button1 = new AskMessage.AskMessageButton()
                    {
                        Text = "Yes"
                    },
                    Button2 = new AskMessage.AskMessageButton()
                    {
                        Text = "No",
                        Url = "/NoMessagePage"
                    }
                };
            }
            return null;
        }

        private bool ProcessAskResponse(HttpRequest request, string nut, int button)
        {
            if (request.Path.StartsWithSegments("/MessageMe/Now"))
            {
                if (button == 1)
                {
                    return true;
                }

                if (button == 2)
                {
                    return false; //Stop authorization
                }
            }
            return false;
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAuthentication();

            app.UseMvc();
        }

        public class SqrlUser
        {
            public string UserId { get; set; }

            public string Suk { get; set; }

            public string Vuk { get; set; }

            public bool Locked { get; set; }

        }

        private static List<SqrlUser> _sqrlUsers = new List<SqrlUser>();

        private static List<string> _sqrlAdminUserIds = new List<string>();

        private void SqrlCreateUser(string userId, string suk, string vuk, HttpContext context)
        {
            _sqrlUsers.Add(new SqrlUser()
            {
                UserId = userId,
                Suk = suk,
                Vuk = vuk
            });
        }

        private void UnlockUser(string userId, HttpContext context)
        {
            _sqrlUsers.Single(x => x.UserId == userId).Locked = false;
        }

        private void LockUser(string userId, HttpContext context)
        {
            _sqrlUsers.Single(x => x.UserId == userId).Locked = true;
        }

        private void RemoveUser(string userId, HttpContext arg2)
        {
            _sqrlUsers.Remove(_sqrlUsers.Single(x => x.UserId == userId));
        }

        private void UpdateUserId(string newUserId, string newSuk, string newVuk, string userId, HttpContext context)
        {
            var user = _sqrlUsers.Single(x => x.UserId == userId);
            user.UserId = newUserId;
            user.Suk = newSuk;
            user.Vuk = newVuk;
        }

        private UserLookUpResult UserExists(string userId, HttpContext context)
        {
            var user = _sqrlUsers.SingleOrDefault(x => x.UserId == userId);
            return user == null ? UserLookUpResult.Unknown : user.Locked ? UserLookUpResult.Disabled : UserLookUpResult.Exists;
        }

        private string GetUserVuk(string userId, HttpContext context)
        {
            return _sqrlUsers.Single(x => x.UserId == userId).Vuk;
        }

        private string GetUserSuk(string userId, HttpContext context)
        {
            return _sqrlUsers.Single(x => x.UserId == userId).Suk;
        }

        private Task OnTicketReceived(TicketReceivedContext context)
        {
            var userId = context.Principal.FindFirst(ClaimTypes.NameIdentifier).Value;
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, _sqrlAdminUserIds.Contains(userId) ? "SqrlAdminRole" : "SqrlUserRole")
            };
            var appIdentity = new ClaimsIdentity(claims);

            context.Principal.AddIdentity(appIdentity);
            return Task.CompletedTask;
        }

    }
}
