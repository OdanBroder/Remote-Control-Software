FROM nginx:alpine

# Create directory for SSL certificates
RUN mkdir -p /etc/nginx/certs

# Copy nginx configuration
COPY nginx.conf /etc/nginx/nginx.conf

# Create self-signed certificate for development
RUN apk add --no-cache openssl && \
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
    -keyout /etc/nginx/certs/railway.key \
    -out /etc/nginx/certs/railway.crt \
    -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"

# Expose ports
EXPOSE 80 443

# Start nginx
CMD ["nginx", "-g", "daemon off;"] 