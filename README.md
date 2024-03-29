---
page_type: sample
languages:
  - csharp
products:
  - aspnet-core
  - microsoft-identity-web
  - azure-active-directory  
name: How to manually validate a JWT access token using the Microsoft identity platform 
urlFragment: active-directory-dotnet-webapi-manual-jwt-validation
services: active-directory
platforms: dotnetcore
author: kalyankrishna1
level: 300
client: .NET Desktop App (WPF)
service: ASP.NET Web API
endpoint: AAD v2.0
---

# How to manually validate a JWT access token using the Microsoft identity platform

- [Overview](#overview)
- [About this sample](#about-this-sample)
- [Scenario: protecting a Web API - acquiring a token for the protected Web API](#scenario-protecting-a-web-api---acquiring-a-token-for-the-protected-web-api)
  - [Token Validation](#token-validation)
  - [What to validate?](#what-to-validate)
  - [Validating the claims](#validating-the-claims)
- [Prerequisites](#prerequisites)
- [Setup](#setup)
  - [Step 1:  Clone or download this repository](#step-1--clone-or-download-this-repository)
  - [Register the sample application(s) with your Azure Active Directory tenant](#register-the-sample-applications-with-your-azure-active-directory-tenant)
  - [Choose the Azure AD tenant where you want to create your applications](#choose-the-azure-ad-tenant-where-you-want-to-create-your-applications)
  - [Register the service app (TodoListService-ManualJwt)](#register-the-service-app-todolistservice-manualjwt)
  - [Register the client app (TodoListClient-ManualJwt)](#register-the-client-app-todolistclient-manualjwt)
- [Running the sample](#running-the-sample)
- [Explore the sample](#explore-the-sample)
- [About The Code](#about-the-code)
  - [Providing your own Custom token validation handler](#providing-your-own-custom-token-validation-handler)
- [How To Recreate This Sample](#how-to-recreate-this-sample)
  - [Creating the TodoListService-ManualJwt Project](#creating-the-todolistservice-manualjwt-project)
  - [Creating the TodoListClient Project](#creating-the-todolistclient-project)
- [How to deploy this sample to Azure](#how-to-deploy-this-sample-to-azure)
  - [Create and publish the `TodoListService-ManualJwt` to an Azure Web Site](#create-and-publish-the-todolistservice-manualjwt-to-an-azure-web-site)
  - [Update the Active Directory tenant application registration for `TodoListService-ManualJwt`](#update-the-active-directory-tenant-application-registration-for-todolistservice-manualjwt)
  - [Update the `TodoListClient-ManualJwt` to call the `TodoListService-ManualJwt` Running in Azure Web Sites](#update-the-todolistclient-manualjwt-to-call-the-todolistservice-manualjwt-running-in-azure-web-sites)
- [Azure Government Deviations](#azure-government-deviations)
- [Troubleshooting](#troubleshooting)
- [Community Help and Support](#community-help-and-support)
- [Contributing](#contributing)
- [More information](#more-information)

![Build badge](https://identitydivision.visualstudio.com/_apis/public/build/definitions/a7934fdd-dcde-4492-a406-7fad6ac00e17/18/badge)

## Overview

This sample demonstrates how to manually validate an access token issued to a web API protected by the Microsoft Identity Platform. Here a .NET Desktop App (WPF) calls a protected ASP.NET Web API that is secured using Azure AD.

## About this sample

A Web API that accepts bearer token as a proof of authentication is secured by [validating the token](https://docs.microsoft.com/en-us/azure/active-directory/develop/access-tokens#validating-tokens) they receive from the callers. When a developer generates a skeleton Web API code using [Visual Studio](https://aka.ms/vsdownload), token validation libraries and code to carry out basic token validation is automatically generated for the project. An example of the generated code using the [asp.net security middleware](https://github.com/aspnet/Security) and [Microsoft Identity Model Extension for .NET](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) to validate tokens is provided below.

```CSharp
public void ConfigureAuth(IAppBuilder app)
{
    app.UseWindowsAzureActiveDirectoryBearerAuthentication(
        new WindowsAzureActiveDirectoryBearerAuthenticationOptions
        {
            Tenant = ConfigurationManager.AppSettings["ida:Tenant"],
            TokenValidationParameters = new TokenValidationParameters {
                    ValidAudience = ConfigurationManager.AppSettings["ida:Audience"]
            },
        });
}
```

The code above will validate the issuer, audience, and the signing tokens of the access token, which is usually sufficient for most scenarios. But often the developer's requirements are more than what these defaults provide. Examples of these requirements can be:

- Restricting the Web API to one or more Apps (App IDs)
- Restricting the Web API to just one or more tenants (Issuers)
- Implement custom authorization.

> Always verify that the access token presented to the Web Api has the expected [scopes or roles](https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-protected-web-api-verification-scope-app-roles#verifying-scopes-in-apis-called-on-behalf-of-users)

This sample demonstrates how to manually process a JWT access token in a web API using the JSON Web Token Handler For the Microsoft .Net Framework.  This sample is equivalent to the [NativeClient-DotNet](https://github.com/Azure-Samples/active-directory-dotnet-native-desktop) sample, except that, in the ``TodoListService``, instead of using OWIN middleware to process the token, the token is processed manually in application code.  The client, which demonstrates how to acquire a token for this protected API, is unchanged from the [NativeClient-DotNet](https://github.com/Azure-Samples/active-directory-dotnet-native-desktop) sample.

![Topology](./ReadmeFiles/Topology.png)

## Scenario: protecting a Web API - acquiring a token for the protected Web API

When you want to protect a Web API, you request your clients to get a [Security token](https://docs.microsoft.com/en-us/azure/active-directory/develop/active-directory-dev-glossary#security-token) for your API, and you validate it. Usually, for ASP.NET applications this validation is delegated to the OWIN middleware, but you can also validate it yourself, leveraging the ``System.IdentityModel.Tokens.Jwt`` library.

### Token Validation

A token represents the outcome of an authentication operation with some artifact that can be unambiguously tied to the Identity Provider that performed the authentication, without relying on any special network infrastructure.

With Azure Active Directory taking the full responsibility of verifying user's raw credentials, the token receiver's responsibility shifts from verifying raw credentials to verifying that their caller did indeed go through your identity provider of choice and successfully authenticated. The identity provider represents successful authentication operations by issuing a token, hence the job now becomes to validate that token.

### What to validate?

While you should always validate tokens issued to the resources (audience) that you are developing, your application will also obtain access tokens for other resources from AAD. AAD will provide an access token in whatever token format that is appropriate to that resource.
This access token itself should be treated like an opaque blob by your application, as your app isn’t the access token’s intended audience and thus your app should not bother itself with looking into the contents of this access token.
Your app should just pass it in the call to the resource. It's the called resource's responsibility to validate this access token.

### Validating the claims

When an application receives an access token upon user sign-in, it should also perform a few checks against the claims in the access token. These verifications include but are not limited to:

- **audience** claim, to verify that the ID token was intended to be given to your application
- **not before** and "expiration time" claims, to verify that the ID token has not expired
- **issuer** claim, to verify that the token was issued to your app by the v2.0 endpoint
- **nonce**, as a token replay attack mitigation

You are advised to use standard library methods like [JwtSecurityTokenHandler.ValidateToken Method (JwtSecurityToken)](https://msdn.microsoft.com/en-us/library/dn451163(v=vs.114).aspx) to do most of the aforementioned heavy lifting. You can further extend the validation process by making decisions based on claims received in the token. For example, multi-tenant applications can extend the standard validation by inspecting the value of the ``tid`` claim (Tenant ID) against a set of pre-selected tenants to ensure they only honor tokens from tenants of their choice. Details on the claims provided in JWT tokens are listed in the [Azure AD token reference](https://docs.microsoft.com/en-us/azure/active-directory/develop/active-directory-token-and-claims). When you debug your application and want to understand the claims held by the token, you might find it useful to use the [JWT token inspector](https://jwt.ms) tool.

> Looking for previous versions of this code sample? Check out the tags on the [releases](../../releases) GitHub page.

>[!Note] If you want to run this sample on **Azure Government**, navigate to the "Azure Government Deviations" section at the bottom of this page.

## Prerequisites

- [Visual Studio](https://visualstudio.microsoft.com/downloads/)
- An **Azure AD** tenant. For more information, see: [How to get an Azure AD tenant](https://docs.microsoft.com/azure/active-directory/develop/quickstart-create-new-tenant)
- A user account in your **Azure AD** tenant. This sample will not work with a **personal Microsoft account**.  If you're signed in to the [Azure portal](https://portal.azure.com) with a personal Microsoft account and have not created a user account in your directory before, you will need to create one before proceeding.

## Setup

### Step 1:  Clone or download this repository

From your shell or command line:

```console
git clone https://github.com/Azure-Samples/active-directory-dotnet-webapi-manual-jwt-validation.git
```

or download and extract the repository .zip file.

> :warning: To avoid path length limitations on Windows, we recommend cloning into a directory near the root of your drive.

### Register the sample application(s) with your Azure Active Directory tenant

There are two projects in this sample. Each needs to be separately registered in your Azure AD tenant. To register these projects, you can:

- follow the steps below for manually register your apps
- or use PowerShell scripts that:
  - **automatically** creates the Azure AD applications and related objects (passwords, permissions, dependencies) for you
  - modify the Visual Studio projects' configuration files.

<details>
  <summary>Expand this section if you want to use this automation:</summary>

> :warning: If you have never used **Azure AD Powershell** before, we recommend you go through the [App Creation Scripts](./AppCreationScripts/AppCreationScripts.md) once to ensure that your environment is prepared correctly for this step.

1. On Windows, run PowerShell as **Administrator** and navigate to the root of the cloned directory
1. If you have never used Azure AD Powershell before, we recommend you go through the [App Creation Scripts](./AppCreationScripts/AppCreationScripts.md) once to ensure that your environment is prepared correctly for this step.
1. In PowerShell run:

   ```PowerShell
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force
   ```

1. Run the script to create your Azure AD application and configure the code of the sample application accordingly.
1. In PowerShell run:

   ```PowerShell
   cd .\AppCreationScripts\
   .\Configure.ps1
   ```

   > Other ways of running the scripts are described in [App Creation Scripts](./AppCreationScripts/AppCreationScripts.md)
   > The scripts also provide a guide to automated application registration, configuration and removal which can help in your CI/CD scenarios.

1. Open the Visual Studio solution and click start to run the code.

</details>

### Choose the Azure AD tenant where you want to create your applications

As a first step you'll need to:

1. Sign in to the [Azure portal](https://portal.azure.com).
1. If your account is present in more than one Azure AD tenant, select your profile at the top right corner in the menu on top of the page, and then **switch directory** to change your portal session to the desired Azure AD tenant.

### Register the service app (TodoListService-ManualJwt)

1. Navigate to the [Azure portal](https://portal.azure.com) and select the **Azure AD** service.
1. Select the **App Registrations** blade on the left, then select **New registration**.
1. In the **Register an application page** that appears, enter your application's registration information:
   - In the **Name** section, enter a meaningful application name that will be displayed to users of the app, for example `TodoListService-ManualJwt`.
   - Under **Supported account types**, select **Accounts in this organizational directory only**.
1. Select **Register** to create the application.
1. In the app's registration screen, find and note the **Application (client) ID**. You use this value in your app's configuration file(s) later in your code.
1. Select **Save** to save your changes.
1. In the app's registration screen, select the **Expose an API** blade to the left to open the page where you can declare the parameters to expose this app as an API for which client applications can obtain [access tokens](https://docs.microsoft.com/azure/active-directory/develop/access-tokens) for.
The first thing that we need to do is to declare the unique [resource](https://docs.microsoft.com/azure/active-directory/develop/v2-oauth2-auth-code-flow) URI that the clients will be using to obtain access tokens for this API. To declare an resource URI, follow the following steps:
   - Select `Set` next to the **Application ID URI** to generate a URI that is unique for this app.
   - For this sample, accept the proposed Application ID URI (`api://{clientId}`) by selecting **Save**.
1. All APIs have to publish a minimum of one [scope](https://docs.microsoft.com/azure/active-directory/develop/v2-oauth2-auth-code-flow#request-an-authorization-code) for the client's to obtain an access token successfully. To publish a scope, follow these steps:
   - Select **Add a scope** button open the **Add a scope** screen and Enter the values as indicated below:
        - For **Scope name**, use `access_as_user`.
        - Select **Admins and users** options for **Who can consent?**.
        - For **Admin consent display name** type `Access TodoListService-ManualJwt`.
        - For **Admin consent description** type `Allows the app to access TodoListService-ManualJwt as the signed-in user.`
        - For **User consent display name** type `Access TodoListService-ManualJwt`.
        - For **User consent description** type `Allow the application to access TodoListService-ManualJwt on your behalf.`
        - Keep **State** as **Enabled**.
        - Select the **Add scope** button on the bottom to save this scope.
1. Select the `Manifest` blade on the left.
   - Set `accessTokenAcceptedVersion` property to **2**.
   - Click on **Save**.

#### Configure the service app (TodoListService-ManualJwt) to use your app registration

Open the project in your IDE (like Visual Studio or Visual Studio Code) to configure the code.

> In the steps below, "ClientID" is the same as "Application ID" or "AppId".

1. Open the `TodoListService-ManualJwt\Web.Config` file.
1. Find the key `ida:TenantId` and replace the existing value with your Azure AD tenant ID.
1. Find the key `ida:Audience` and replace the existing value with the App ID URI you registered earlier, when exposing an API. For instance use `api://<application_id>`.
1. Find the key `ida:ClientId` and replace the existing value with the application ID (clientId) of `TodoListService-ManualJwt` app copied from the Azure portal.

### Register the client app (TodoListClient-ManualJwt)

1. Navigate to the [Azure portal](https://portal.azure.com) and select the **Azure AD** service.
1. Select the **App Registrations** blade on the left, then select **New registration**.
1. In the **Register an application page** that appears, enter your application's registration information:
   - In the **Name** section, enter a meaningful application name that will be displayed to users of the app, for example `TodoListClient-ManualJwt`.
   - Under **Supported account types**, select **Accounts in this organizational directory only**.
1. Select **Register** to create the application.
1. In the app's registration screen, find and note the **Application (client) ID**. You use this value in your app's configuration file(s) later in your code.
1. In the app's registration screen, select **Authentication** in the menu.
   - If you don't have a platform added, select **Add a platform** and select the **Public client (mobile & desktop)** option.
   - In the **Redirect URIs** | **Suggested Redirect URIs for public clients (mobile, desktop)** section, select **https://login.microsoftonline.com/common/oauth2/nativeclient**
1. Select **Save** to save your changes.
1. In the app's registration screen, select the **API permissions** blade in the left to open the page where we add access to the APIs that your application needs.
   - Select the **Add a permission** button and then,
   - Ensure that the **My APIs** tab is selected.
   - In the list of APIs, select the API `TodoListService-ManualJwt`.
   - In the **Delegated permissions** section, select the **Access 'TodoListService-ManualJwt'** in the list. Use the search box if necessary.
   - Select the **Add permissions** button at the bottom.

#### Configure the client app (TodoListClient-ManualJwt) to use your app registration

Open the project in your IDE (like Visual Studio or Visual Studio Code) to configure the code.

> In the steps below, "ClientID" is the same as "Application ID" or "AppId".

1. Open the `TodoListClient\App.Config` file.
1. Find the key `ida:Tenant` and replace the existing value with your Azure AD tenant name.
1. Find the key `ida:ClientId` and replace the existing value with the application ID (clientId) of `TodoListClient-ManualJwt` app copied from the Azure portal.
1. Find the key `todo:TodoListResourceId` and replace the existing value with the App ID URI you registered earlier, when exposing an API. For instance use `api://<application_id>`.
1. Find the key `todo:TodoListBaseAddress` and replace the existing value with the base address of `TodoListService-ManualJwt` (by default `https://localhost:44324`).

## Running the sample

> For Visual Studio Users
>
> Clean the solution, rebuild the solution, and run it.  You might want to go into the solution properties and set both projects as startup projects, with the service project starting first.

## Explore the sample

Explore the sample by signing in, adding items to the To Do list, removing the user account, and starting again.  Notice that if you stop the application without removing the user account, the next time you run the application you won't be prompted to sign in again - that is the sample implements a [persistent cache for MSAL](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/token-cache-serialization), and remembers the tokens from the previous run.

> :information_source:  Did the sample not work for you as expected? Did you encounter issues trying this sample? Then please reach out to us using the [GitHub Issues](../../issues) page.

> [Consider taking a moment to share your experience with us.](https://forms.office.com/Pages/ResponsePage.aspx?id=v4j5cvGGr0GRqy180BHbR73pcsbpbxNJuZCMKN0lURpUMjFRQjA0RElFUFNPV0dCUVBGQzk0QkhKTiQlQCN0PWcu)

## About The Code

The manual JWT validation occurs in the [TokenValidationHandler](https://github.com/Azure-Samples/active-directory-dotnet-webapi-manual-jwt-validation/blob/master/TodoListService-ManualJwt/Global.asax.cs#L58) implementation in the `Global.aspx.cs` file in the TodoListService-ManualJwt project. 
Each time a call is made to the web API, the [TokenValidationHandler.SendAsync()](https://github.com/Azure-Samples/active-directory-dotnet-webapi-manual-jwt-validation/blob/4b80657c5506c8cb30af67b9f61bb6aa68dfca58/TodoListService-ManualJwt/Global.asax.cs#L80) handler is executed:

This method:

1. Gets the token from the Authorization headers
1. Gets the open ID configuration, including keys from the Azure AD discovery endpoint
1. Sets the parameters to validate in `GetTokenValidationParameters()`
    - the audience - the application accepts both its App ID URI and its AppID/clientID
    - the valid issuers - the application accepts both Azure AD V1 and Azure AD V2
1. Then the token is validated
1. An asp.net claims principal is created after a successful validation
1. ensures that the web API is consented to and provisioned in the Azure AD tenant from where the access token originated
1. Finally, a check for scopes that the web API expects from the caller is carried out

The `TokenValidationHandler` class is registered with ASP.NET in the `TodoListService-ManualJwt/Global.asx.cs` file, in the [Application_Start()](https://github.com/Azure-Samples/active-directory-dotnet-webapi-manual-jwt-validation/blob/4b80657c5506c8cb30af67b9f61bb6aa68dfca58/TodoListService-ManualJwt/Global.asax.cs#L54) method.

For more validation options, please refer to [TokenValidationParameters.cs](https://docs.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters?view=azure-dotnet)

### Providing your own Custom token validation handler

If you do not wish to control the token validation from its very beginning to the end as laid out in the `Global.asax.cs`, but only limit yourself to validate business logic based on claims in the presented token, you can craft a Custom token handler as provided in the example below.  
The provided example, validates to allow callers from a list of whitelisted tenants only.

1. Create a custom handler implementation

```CSharp

public class CustomTokenHandler : JwtSecurityTokenHandler
{
   public override ClaimsPrincipal ValidateToken(
      string token, TokenValidationParameters validationParameters,
      out SecurityToken validatedToken)
   {
      try
      {
            var claimsPrincipal = base.ValidateToken(token, validationParameters, out validatedToken);

            string[] allowedTenants = { "14c2f153-90a7-4689-9db7-9543bf084dad", "af8cc1a0-d2aa-4ca7-b829-00d361edb652", "979f4440-75dc-4664-b2e1-2cafa0ac67d1" };
            string tenantId = claimsPrincipal.Claims.FirstOrDefault(x => x.Type == "tid" || x.Type == "http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

            if (!allowedTenants.Contains(tenantId))
            {
               throw new Exception("This tenant is not authorized");
            }

            return claimsPrincipal;
      }
      catch (Exception e)
      {            
            throw;
      }
   }
}

   ```

1. Assign the custom handler in `Startup.Auth.cs`

  ```CSharp

app.UseWindowsAzureActiveDirectoryBearerAuthentication(
               new WindowsAzureActiveDirectoryBearerAuthenticationOptions
               {
                  Tenant = ConfigurationManager.AppSettings["ida:Tenant"],
                  TokenValidationParameters = new TokenValidationParameters
                  {
                     ValidAudience = ConfigurationManager.AppSettings["ida:Audience"]
                  },
                  TokenHandler = new CustomTokenHandler()
               });
   ```

## How To Recreate This Sample

First, in Visual Studio 2017 create an empty solution to host the projects.  Then, follow these steps to create each project.

### Creating the TodoListService-ManualJwt Project

1. In Visual Studio, create a new `Visual C#` `ASP.NET Web Application (.NET Framework)`. Choose `Web Api` in the next screen. Leave the project's chosen authentication mode as the default, that is, `No Authentication`".
1. Set SSL Enabled to be True. Note the SSL URL.
1. In the project properties, Web properties, set the Project Url to be the SSL URL.
1. Add the latest stable JSON Web Token Handler For the Microsoft .Net Framework NuGet, System.IdentityModel.Tokens.Jwt, to the project.
1. Add an assembly reference to `System.IdentityModel`.
1. In the `Models` folder, add a new class named `TodoItem.cs`.  Copy the implementation of TodoItem from this sample into the class.
1. Create a folder named `Utils` , add a new class named `ClaimConstants.cs`.  Copy the implementation of `ClaimConstants.cs` from this sample into the class.
1. Add a new, empty, Web API 2 controller named `TodoListController`.
1. Copy the implementation of the TodoListController from this sample into the controller.
1. Open Global.asax, and copy the implementation from this sample into the controller.  Note that a single line is added at the end of `Application_Start()`,

      ```CSharp
      GlobalConfiguration.Configuration.MessageHandlers.Add(new TokenValidationHandler());
      ```

1. In `web.config` create keys for `ida:AADInstance`, `ida:Tenant`, and `ida:Audience` and set them accordingly.  For the global Azure cloud, the value of `ida:AADInstance` is `https://login.microsoftonline.com/{0}`.

### Creating the TodoListClient Project

1. In the solution, create a new Windows --> Windows Classic Desktop -> WPF App(.NET Framework)  called TodoListClient.
2. Add the Microsoft Authentication Library (MSAL) NuGet, `Microsoft.Identity.Client` to the project.
3. Add  assembly references to `System.Net.Http`, `System.Web.Extensions`, and `System.Configuration`.
4. Add a new class to the project named `TodoItem.cs`.  Copy the code from the sample project file of the same name into this class, completely replacing the code in the file in the new project.
5. Add a new class to the project named `FileCache.cs`.  Copy the code from the sample project file of the same name into this class, completely replacing the code in the file in the new project.
6. Copy the markup from `MainWindow.xaml` in the sample project into the file of the same name in the new project, completely replacing the markup in the file in the new project.
7. Copy the code from `MainWindow.xaml.cs` in the sample project into the file of the same name in the new project, completely replacing the code in the file in the new project.
8. In `app.config` create keys for `ida:AADInstance`, `ida:Tenant`, `ida:ClientId`, `todo:TodoListResourceId`, and `todo:TodoListBaseAddress` and set them accordingly.  For the global Azure cloud, the value of `ida:AADInstance` is `https://login.microsoftonline.com/{0}`.

Finally, in the properties of the solution itself, set both projects as startup projects.

## How to deploy this sample to Azure

This project has one WebApp / Web API projects. To deploy them to Azure Web Sites, you'll need, for each one, to:

- create an Azure Web Site
- publish the Web App / Web APIs to the web site, and
- update its client(s) to call the web site instead of IIS Express.

### Create and publish the `TodoListService-ManualJwt` to an Azure Web Site

1. Sign in to the [Azure portal](https://portal.azure.com).
1. Click `Create a resource` in the top left-hand corner, select **Web** --> **Web App**, and give your web site a name, for example, `TodoListService-ManualJwt-contoso.azurewebsites.net`.
1. Thereafter select the `Subscription`, `Resource Group`, `App service plan and Location`. `OS` will be **Windows** and `Publish` will be **Code**.
1. Click `Create` and wait for the App Service to be created.
1. Once you get the `Deployment succeeded` notification, then click on `Go to resource` to navigate to the newly created App service.

1. From the **Overview** tab of the App Service, download the publish profile by clicking the **Get publish profile** link and save it.  Other deployment mechanisms, such as from source control, can also be used.
1. Switch to Visual Studio and go to the TodoListService-ManualJwt project.  Right click on the project in the Solution Explorer and select **Publish**.  Click **Import Profile** on the bottom bar, and import the publish profile that you downloaded earlier.
1. Click on **Configure** and in the `Connection tab`, update the Destination URL so that it is a `https` in the home page url, for example [https://TodoListService-ManualJwt-contoso.azurewebsites.net](https://TodoListService-ManualJwt-contoso.azurewebsites.net). Click **Next**.
1. On the Settings tab, make sure `Enable Organizational Authentication` is NOT selected.  Click **Save**. Click on **Publish** on the main screen.
1. Visual Studio will publish the project and automatically open a browser to the URL of the project.  If you see the default web page of the project, the publication was successful.

### Update the Active Directory tenant application registration for `TodoListService-ManualJwt`

1. Navigate back to to the [Azure portal](https://portal.azure.com).
In the left-hand navigation pane, select the **Azure Active Directory** service, and then select **App registrations (Preview)**.
1. In the resultant screen, select the `TodoListService-ManualJwt` application.
1. From the *Branding* menu, update the **Home page URL**, to the address of your service, for example [https://TodoListService-ManualJwt-contoso.azurewebsites.net](https://TodoListService-ManualJwt-contoso.azurewebsites.net). Save the configuration.
1. Add the same URL in the list of values of the *Authentication -> Redirect URIs* menu. If you have multiple redirect urls, make sure that there a new entry using the App service's Uri for each redirect url.

### Update the `TodoListClient-ManualJwt` to call the `TodoListService-ManualJwt` Running in Azure Web Sites

1. In Visual Studio, go to the `TodoListClient-ManualJwt` project.
2. Open `TodoListClient\App.Config`.  Only one change is needed - update the `todo:TodoListBaseAddress` key value to be the address of the website you published,
   for example, [https://TodoListService-ManualJwt-contoso.azurewebsites.net](https://TodoListService-ManualJwt-contoso.azurewebsites.net).
3. Run the client! If you are trying multiple different client types (for example, .Net, Windows Store, Android, iOS) you can have them all call this one published web API.

> NOTE: Remember, the To Do list is stored in memory in this TodoListService sample. Azure Web Sites will spin down your web site if it is inactive, and your To Do list will get emptied.
Also, if you increase the instance count of the web site, requests will be distributed among the instances. To Do will, therefore, not be the same on each instance.

## Azure Government Deviations

In order to run this sample on Azure Government, you can follow through the steps above with a few variations:

- Step 2:
  - You must register this sample for your AAD Tenant in Azure Government by following Step 2 above in the [Azure Government portal](https://portal.azure.us).
- Step 3:

  - Before configuring the sample, you must make sure your [Visual Studio is connected to Azure Government](https://docs.microsoft.com/azure/azure-government/documentation-government-get-started-connect-with-vs).
  - Navigate to the Web.config file. Replace the `ida:AADInstance` property in the Azure AD section with `https://login.microsoftonline.us/`.

Once those changes have been accounted for, you should be able to run this sample on Azure Government.

## Troubleshooting

If you are using this sample with an Azure AD B2C custom policy, you might want to read #22, and change step 3. in the [About The Code](#about-the-code) paragraph.

## Community Help and Support

Use [Stack Overflow](http://stackoverflow.com/questions/tagged/msal) to get support from the community.
Ask your questions on Stack Overflow first and browse existing issues to see if someone has asked your question before.
Make sure that your questions or comments are tagged with [`msal` `dotnet` `azure-active-directory`].

If you find a bug in the sample, please raise the issue on [GitHub Issues](../../issues).

To provide a recommendation, visit the following [User Voice page](https://feedback.azure.com/forums/169401-azure-active-directory).

## Contributing

If you'd like to contribute to this sample, see [CONTRIBUTING.MD](/CONTRIBUTING.md).

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information, see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## More information

- [Microsoft identity platform (Azure Active Directory for developers)](https://docs.microsoft.com/azure/active-directory/develop/)
- [MSAL.NET's conceptual documentation](https://aka.ms/msal-net)
- [Quickstart: Register an application with the Microsoft identity platform](https://docs.microsoft.com/azure/active-directory/develop/quickstart-register-app)
- [Quickstart: Configure a client application to access web APIs](https://docs.microsoft.com/azure/active-directory/develop/quickstart-configure-app-access-web-apis)
- [Recommended pattern to acquire a token](https://github.com/AzureAD/azure-activedirectory-library-for-dotnet/wiki/AcquireTokenSilentAsync-using-a-cached-token#recommended-pattern-to-acquire-a-token)
- [Token Cache serialization](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/token-cache-serialization)

For more information about token validation, see:

- [Principles of Token validation](https://docs.microsoft.com/en-us/azure/active-directory/develop/access-tokens#validating-tokens)
- [Microsoft Identity Model Extension for .NET](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)
- [Protect a web API](https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-protected-web-api-overview)
- [Verify scopes and roles in Web API](https://docs.microsoft.com/en-us/azure/active-directory/develop/scenario-protected-web-api-verification-scope-app-roles)
- [TokenValidationParameters.cs](https://docs.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters?view=azure-dotnet)

For more information about how the protocols work in this scenario and other scenarios, see [Authentication Scenarios for Azure AD](http://go.microsoft.com/fwlink/?LinkId=394414).
