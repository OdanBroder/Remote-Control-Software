FROM nginx:alpine

# Copy Nginx configuration
COPY ./nginx.conf /etc/nginx/nginx.conf

# Create directory for SSL certificates
RUN mkdir -p /etc/nginx/certs

# Expose ports
EXPOSE 80 443 