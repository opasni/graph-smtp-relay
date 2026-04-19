FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY GraphSmtpRelay.csproj ./
RUN dotnet restore -a $TARGETARCH

COPY . ./
RUN dotnet publish -c Release -a $TARGETARCH -o /app/publish /p:UseAppHost=false --no-restore

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

EXPOSE 2525
EXPOSE 8080

ENTRYPOINT ["dotnet", "GraphSmtpRelay.dll"]