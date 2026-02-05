# Multi-Stage Dockerfile for File Simulator Dashboard
#
# Stage 1: Build the React application with Vite
# Stage 2: Serve static assets with nginx
#
# Build Arguments:
#   VITE_API_BASE_URL - API endpoint for Control API (default: http://file-simulator.local:30500)
#
# Example Usage:
#   docker build -t file-simulator-dashboard:latest .
#   docker build --build-arg VITE_API_BASE_URL=http://custom-api:5000 -t file-simulator-dashboard:latest .
#   docker run -d -p 80:80 file-simulator-dashboard:latest

# ============================================================================
# Stage 1: Builder - Compile TypeScript and bundle with Vite
# ============================================================================
FROM node:20-alpine AS builder

# Set working directory
WORKDIR /app

# Copy package files for dependency installation
# This layer is cached unless package files change
COPY package*.json ./

# Install dependencies using clean install for reproducible builds
# npm ci is faster and more reliable than npm install for CI/CD
RUN npm ci

# Copy source code
COPY . .

# Build argument for API endpoint configuration
# This is embedded into the JavaScript bundle at build time
ARG VITE_API_BASE_URL=http://file-simulator.local:30500
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# Build the application
# Vite outputs optimized production build to dist/
# - Minified JavaScript and CSS
# - Content-hashed filenames for cache busting
# - Tree-shaken to remove unused code
RUN npm run build

# ============================================================================
# Stage 2: Production - Serve static files with nginx
# ============================================================================
FROM nginx:alpine AS production

# Copy built assets from builder stage
# This keeps the final image small (~25-30MB total)
COPY --from=builder /app/dist /usr/share/nginx/html

# Copy custom nginx configuration
# Replaces default.conf with SPA routing and health endpoint
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose HTTP port
EXPOSE 80

# Add health check for Kubernetes readiness/liveness probes
# Checks /health endpoint every 30 seconds
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost/health || exit 1

# Start nginx in foreground mode
# daemon off prevents nginx from detaching, required for containers
CMD ["nginx", "-g", "daemon off;"]
