<p align="center">
  <img width="256" height="256" alt="Logo Black" src="https://github.com/user-attachments/assets/2a65581c-c821-4eab-859b-7d111cd3e39f" />
</p>
<h1 align="center"> AMDiscordRPC </h1>
<p align="center"><i>An another Apple Music Discord RPC</i></p>

[Support Server](https://discord.gg/5trvjuqgm8)

## Usage
[Download](https://github.com/CrawLeyYou/AMDiscordRPC/releases/latest) latest release and make sure you have [.NET Framework 4.7.2](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472).

## Development
You need to install all NuGet packages and .NET Framework 4.7.2. (You need to install [DiscordRPC](https://github.com/Lachee/discord-rpc-csharp/releases/download/v1.3.0/DiscordRichPresence.1.3.0.28.nupkg) package independently)

## How it looks like
https://github.com/user-attachments/assets/ccc28977-de7f-49b5-a1c2-c90c68a00387

# How to use Animated Covers
## Cloudflare Side
First you need these:

-Domain

-Cloudflare account(or aws)

You need to add your domain to Cloudflare then create R2 bucket from here
![image](https://github.com/user-attachments/assets/9b925055-d5df-4ca7-bb1e-1240d576dc7a)
![image](https://github.com/user-attachments/assets/f7316901-c6c5-471d-bed8-eea041e038b9)

After that go to your R2 bucket settings and add your domain from here
![image](https://github.com/user-attachments/assets/6d30b4d5-6cf7-4b2a-96c0-488513fc29c4)

Then return to first page and click here to create account API Token. (I recommend these settings)
![image](https://github.com/user-attachments/assets/f38c8320-40df-4dad-988e-37036eb73b05)

After creating the API key save these
![image](https://github.com/user-attachments/assets/49713dfe-5b08-4bd5-a011-63f53568024b)

## Adding values to app
You need to launch 1.3.5+ version atleast once to create Database for the program.

After the startup you can interact with S3 Credentials Menu by right clicking icon of the application then selecting S3 Credentials option. 

## License
This project is licensed under [MIT](https://opensource.org/license/MIT) license.
