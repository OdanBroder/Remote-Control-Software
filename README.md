# Remote Control Software

A comprehensive real-time remote desktop control application built with C# WPF client and ASP.NET Core server. Features secure authentication, WebRTC-based screen sharing, file transfer capabilities, and real-time communication using SignalR.

## 🚀 Features

### Core Functionality
- **Real-time Screen Sharing**: WebRTC-based screen capture and streaming with high performance
- **Remote Control**: Full mouse and keyboard input forwarding
- **File Transfer**: Secure file upload/download between connected devices
- **User Authentication**: JWT-based authentication with secure user registration and login
- **Session Management**: Create, join, and manage remote control sessions
- **Real-time Communication**: SignalR hub for instant communication and control

### Security & Performance
- **Encrypted Communication**: SSL/TLS encryption for all data transmission
- **JWT Authentication**: Secure token-based user authentication
- **Input Validation**: Comprehensive input validation and sanitization
- **Cross-platform Server**: Docker containerization for easy deployment
- **Database Support**: MySQL database with Entity Framework Core
- **Redis Caching**: Redis integration for session management and caching

### Technical Features
- **Modern UI**: WPF application with custom controls and styling
- **Video Compression**: Optimized I420A video format for efficient streaming
- **Audio Support**: Audio controller for future audio streaming capabilities
- **Scalable Architecture**: Microservices-ready architecture with SignalR scaling

## 🏗️ Architecture

### Client Application (WPF - .NET Framework 4.8)
```
Client/
├── Views/              # WPF Views (Login, Register, Connect, Screen Capture, File Transfer)
├── ViewModels/         # MVVM ViewModels
├── Services/           # Core services (WebRTC, SignalR, Auth, File Transfer)
├── Models/             # Data models and DTOs
├── Helpers/            # Utility classes and storage helpers
├── CustomControls/     # Custom WPF controls
└── Styles/             # UI styling and themes
```

### Server Application (ASP.NET Core 8.0)
```
Server/
├── Controllers/        # API Controllers (Auth, Session, File Transfer, Audio)
├── Hubs/              # SignalR hubs for real-time communication
├── Services/          # Business logic services
├── Models/            # Entity models and DTOs
├── Data/              # Database context and configurations
├── Middleware/        # Custom middleware components
└── Migrations/        # Entity Framework migrations
```

## 🛠️ Technology Stack

### Client
- **Framework**: .NET Framework 4.8
- **UI**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM Pattern
- **WebRTC**: Microsoft MixedReality WebRTC
- **Real-time**: SignalR Client
- **Networking**: HttpClient with custom SSL validation

### Server
- **Framework**: ASP.NET Core 8.0
- **Database**: MySQL with Entity Framework Core
- **Caching**: Redis (StackExchange.Redis)
- **Authentication**: JWT Bearer tokens
- **Real-time**: SignalR with MessagePack protocol
- **Security**: BCrypt password hashing
- **Logging**: Serilog
- **Containerization**: Docker & Docker Compose

### Dependencies & Libraries
- **Screen Capture**: Custom DXGICaptureCore and ScreenCaptureLibrary
- **Video Processing**: libyuv for video format conversion
- **Encryption**: Built-in cryptographic services
- **Configuration**: Environment variables with dotenv.net

## 📋 Prerequisites

### Client Requirements
- Windows 10/11
- .NET Framework 4.8
- Visual Studio 2019 or later (for development)

### Server Requirements
- .NET 8.0 SDK
- MySQL 8.0+
- Redis (optional, for scaling)
- Docker & Docker Compose (for containerized deployment)

## 🚀 Installation & Setup

### 1. Clone the Repository
```bash
git clone https://github.com/yourusername/Remote-Control-Software.git
cd Remote-Control-Software
```

### 2. Server Setup

#### Using Docker (Recommended)
1. Navigate to the server directory:
```bash
cd Server
```

2. Create a `.env` file with your configuration:
```env
DB_HOST=db
DB_PORT=3306
DB_NAME=RemoteControl_DB
DB_USER=remotecontrol_user
DB_PASSWORD=your_secure_password
MYSQL_ROOT_PASSWORD=your_root_password
JWT_SECRET=your_jwt_secret_key_here
ASPNETCORE_ENVIRONMENT=Production
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_USER=
REDIS_PASSWORD=
```

3. Build and run with Docker Compose:
```bash
docker-compose up -d
```

#### Manual Setup
1. Install MySQL and create a database
2. Update `appsettings.json` with your database connection string
3. Run Entity Framework migrations:
```bash
dotnet ef database update
```
4. Start the server:
```bash
dotnet run
```

### 3. Client Setup

1. Open `Client/Client.sln` in Visual Studio
2. Update `App.config` with your server URL if needed
3. Build and run the application

## 🎯 Usage

### For Hosts (Screen Sharing)
1. **Register/Login**: Create an account or log in to the application
2. **Start Session**: Click "Start Session" to begin hosting
3. **Share Session Code**: Provide the generated session code to the client
4. **Accept Connection**: Approve incoming connection requests
5. **Control Access**: Monitor and manage connected clients

### For Clients (Remote Control)
1. **Register/Login**: Create an account or log in to the application
2. **Join Session**: Enter the session code provided by the host
3. **Connect**: Establish connection to the remote desktop
4. **Control**: Use mouse and keyboard to control the remote system
5. **File Transfer**: Transfer files between local and remote systems

### File Transfer
- **Upload**: Select files to transfer to the remote system
- **Download**: Receive files from the remote system
- **Progress Tracking**: Monitor transfer progress in real-time

## 🔧 Configuration

### Server Configuration
Key configuration options in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=RemoteControlDb;user=root;password=yourpassword;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Environment Variables
- `DB_HOST`: Database host address
- `DB_PORT`: Database port
- `DB_NAME`: Database name
- `DB_USER`: Database username
- `DB_PASSWORD`: Database password
- `JWT_SECRET`: JWT signing secret
- `REDIS_HOST`: Redis host (for scaling)

### Client Configuration
Configure server endpoints in `App.config` or through the application settings.

## 🔒 Security Features

- **JWT Authentication**: Secure token-based authentication
- **Password Hashing**: BCrypt for secure password storage
- **SSL/TLS Encryption**: All communications encrypted
- **Input Validation**: Comprehensive validation on all inputs
- **Session Management**: Secure session handling and cleanup
- **CORS Protection**: Configured CORS policies

## 🧪 Testing

### Running Tests
```bash
# Server tests
cd Server
dotnet test

# Client tests
cd Client/tests
# Run through Visual Studio Test Explorer
```

## 📊 Performance Optimizations

- **Video Compression**: Efficient I420A format for video streaming
- **Memory Management**: Proper disposal of resources and memory cleanup
- **Connection Pooling**: Optimized database and Redis connections
- **Caching**: Redis caching for frequently accessed data
- **Async Operations**: Non-blocking operations throughout the application

## 🐳 Docker Deployment

The application includes Docker support for easy deployment:

- **Multi-container Setup**: Separate containers for app, database, and cache
- **Health Checks**: Built-in health monitoring
- **Volume Persistence**: Data persistence for database
- **Network Isolation**: Secure container networking
- **Environment Configuration**: Flexible environment-based configuration

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License .

## 📞 Support

For support and questions:
- Create an issue in the GitHub repository
- Check the documentation in the `/docs` folder
- Review the API documentation at `/swagger` when running the server

## 🔮 Future Enhancements

- Multi-monitor support
- Audio streaming
- Mobile client applications
- File synchronization
- Advanced user permissions
- Session recording and playback
- Integration with popular cloud storage services

---

**Built with ❤️ for secure and efficient remote desktop control**
