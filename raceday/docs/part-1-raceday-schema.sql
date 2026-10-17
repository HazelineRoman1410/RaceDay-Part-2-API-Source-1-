-- Reference schema for Part 2 EF Core code-first model.
-- Full planning version is preserved in docs/part-1-complete.html.
CREATE DATABASE RaceDayDb;
GO
USE RaceDayDb;
GO
CREATE TABLE Users (
 UserId INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
 FirstName NVARCHAR(80) NOT NULL, LastName NVARCHAR(80) NOT NULL,
 Email NVARCHAR(255) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
 PasswordHash NVARCHAR(255) NOT NULL,
 Role NVARCHAR(20) NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser','Participant')),
 Phone NVARCHAR(30) NULL, ProfileImageUrl NVARCHAR(500) NULL,
 CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO
CREATE TABLE Events (
 EventId INT IDENTITY(1,1) CONSTRAINT PK_Events PRIMARY KEY,
 OrganiserId INT NOT NULL, Name NVARCHAR(160) NOT NULL, Description NVARCHAR(2000) NOT NULL,
 EventDate DATETIME2 NOT NULL, Location NVARCHAR(160) NOT NULL,
 DistanceKm DECIMAL(6,2) NOT NULL CONSTRAINT CK_Events_Distance CHECK (DistanceKm > 0),
 EventType NVARCHAR(20) NOT NULL CONSTRAINT CK_Events_Type CHECK (EventType IN ('Run','Walk','Cycle')),
 BannerImageUrl NVARCHAR(500) NULL,
 CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Events_CreatedAt DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserId) REFERENCES Users(UserId)
);
GO
CREATE TABLE Categories (
 CategoryId INT IDENTITY(1,1) CONSTRAINT PK_Categories PRIMARY KEY,
 EventId INT NOT NULL, Name NVARCHAR(100) NOT NULL,
 CategoryType NVARCHAR(20) NOT NULL CONSTRAINT CK_Categories_Type CHECK (CategoryType IN ('Age','Distance')),
 MinimumAge TINYINT NULL, MaximumAge TINYINT NULL, DistanceKm DECIMAL(6,2) NULL,
 EntryFee DECIMAL(10,2) NOT NULL CONSTRAINT DF_Categories_Fee DEFAULT 0,
 CONSTRAINT FK_Categories_Event FOREIGN KEY (EventId) REFERENCES Events(EventId),
 CONSTRAINT UQ_Categories_Event_Name UNIQUE (EventId, Name),
 CONSTRAINT CK_Categories_AgeRange CHECK (MinimumAge IS NULL OR MaximumAge IS NULL OR MinimumAge <= MaximumAge)
);
GO
CREATE TABLE Enrolments (
 EnrolmentId INT IDENTITY(1,1) CONSTRAINT PK_Enrolments PRIMARY KEY,
 ParticipantId INT NOT NULL, EventId INT NOT NULL, CategoryId INT NOT NULL,
 Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Enrolments_Status DEFAULT 'Pending',
 EnrolledAt DATETIME2 NOT NULL CONSTRAINT DF_Enrolments_EnrolledAt DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending','Confirmed','Cancelled')),
 CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantId) REFERENCES Users(UserId),
 CONSTRAINT FK_Enrolments_Event FOREIGN KEY (EventId) REFERENCES Events(EventId),
 CONSTRAINT FK_Enrolments_Category FOREIGN KEY (CategoryId) REFERENCES Categories(CategoryId),
 CONSTRAINT UQ_Enrolments_Participant_Event UNIQUE (ParticipantId, EventId)
);
GO
CREATE TABLE Results (
 ResultId INT IDENTITY(1,1) CONSTRAINT PK_Results PRIMARY KEY,
 EnrolmentId INT NOT NULL CONSTRAINT UQ_Results_Enrolment UNIQUE,
 FinishTime TIME(0) NOT NULL, Position INT NOT NULL CONSTRAINT CK_Results_Position CHECK (Position > 0),
 CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES Enrolments(EnrolmentId)
);
GO
CREATE TABLE WeatherSnapshots (
 WeatherId INT IDENTITY(1,1) CONSTRAINT PK_WeatherSnapshots PRIMARY KEY,
 EventId INT NOT NULL, ObservedAt DATETIME2 NOT NULL, TemperatureC DECIMAL(5,2) NOT NULL,
 Conditions NVARCHAR(120) NOT NULL, WindKph DECIMAL(5,2) NULL, RainChancePercent TINYINT NULL,
 CONSTRAINT FK_WeatherSnapshots_Event FOREIGN KEY (EventId) REFERENCES Events(EventId)
);
GO
CREATE TABLE RoutePoints (
 RoutePointId INT IDENTITY(1,1) CONSTRAINT PK_RoutePoints PRIMARY KEY,
 EventId INT NOT NULL, SequenceNo INT NOT NULL, Latitude DECIMAL(9,6) NOT NULL,
 Longitude DECIMAL(9,6) NOT NULL, ElevationMetres DECIMAL(7,2) NULL,
 CONSTRAINT FK_RoutePoints_Event FOREIGN KEY (EventId) REFERENCES Events(EventId),
 CONSTRAINT UQ_RoutePoints_Event_Sequence UNIQUE (EventId, SequenceNo)
);
GO
