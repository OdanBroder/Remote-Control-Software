# Load environment variables from .env file
include .env
export

# Docker Compose commands
.PHONY: up down build rebuild logs clean

# Start all services
up:
	docker-compose up -d

# Stop all services
down:
	docker-compose down

# Build all services
build:
	docker compose up -d --build

# Rebuild and restart all services
rebuild: down build up

# View logs
logs:
	docker-compose logs -f

# Clean up Docker resources
clean:
	docker-compose down -v
	docker system prune -f

# Build individual services
build-server:
	docker-compose build server

build-nginx:
	docker-compose build nginx

# Scale server instances
scale-server:
	docker-compose up -d --scale server=$(SERVER_INSTANCES)

# Help command
help:
	@echo "Available commands:"
	@echo "  make up          - Start all services"
	@echo "  make down        - Stop all services"
	@echo "  make build       - Build all services"
	@echo "  make rebuild     - Rebuild and restart all services"
	@echo "  make logs        - View logs"
	@echo "  make clean       - Clean up Docker resources"
	@echo "  make build-server - Build server service"
	@echo "  make build-nginx  - Build nginx service"
	@echo "  make scale-server - Scale server instances" 