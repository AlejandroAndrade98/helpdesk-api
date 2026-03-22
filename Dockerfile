FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY HelpDeskApi.csproj ./
RUN dotnet restore "HelpDeskApi.csproj"

COPY . ./
RUN dotnet publish "HelpDeskApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "HelpDeskApi.dll"]