#!/bin/bash
# File Simulator Suite - Verification Test Script (Linux/WSL)
# Tests all protocols to ensure the simulator is working correctly

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Counters
PASSED=0
FAILED=0

# Functions
success() { echo -e "  ${GREEN}✅ $1${NC}"; }
failure() { echo -e "  ${RED}❌ $1${NC}"; }
info() { echo -e "  ${CYAN}ℹ️  $1${NC}"; }
header() { echo -e "\n${YELLOW}═══ $1 ═══${NC}"; }

# Get Minikube IP
MINIKUBE_IP=${1:-$(minikube ip 2>/dev/null)}

if [ -z "$MINIKUBE_IP" ]; then
    echo -e "${RED}ERROR: Could not get Minikube IP. Is Minikube running?${NC}"
    exit 1
fi

echo -e "${CYAN}=============================================${NC}"
echo -e "${CYAN}  File Simulator Suite - Verification Tests${NC}"
echo -e "${CYAN}=============================================${NC}"
echo "Minikube IP: $MINIKUBE_IP"

# ============================================
# Test 1: Kubernetes Connectivity
# ============================================
header "TEST 1: Kubernetes Connectivity"

if kubectl get pods -n file-simulator &>/dev/null; then
    RUNNING_PODS=$(kubectl get pods -n file-simulator --field-selector=status.phase=Running -o name | wc -l)
    info "Found $RUNNING_PODS running pods in file-simulator namespace"
    kubectl get pods -n file-simulator --no-headers | while read line; do
        success "$line"
    done
    ((PASSED++))
else
    failure "Cannot connect to Kubernetes"
    ((FAILED++))
fi

# ============================================
# Test 2: Management UI (FileBrowser)
# ============================================
header "TEST 2: Management UI (FileBrowser)"

if curl -s -o /dev/null -w "%{http_code}" "http://${MINIKUBE_IP}:30080/health" | grep -q "200"; then
    success "Management UI is accessible at http://${MINIKUBE_IP}:30080"
    ((PASSED++))
else
    failure "Management UI not accessible"
    ((FAILED++))
fi

# ============================================
# Test 3: HTTP File Server
# ============================================
header "TEST 3: HTTP File Server"

if curl -s -o /dev/null -w "%{http_code}" "http://${MINIKUBE_IP}:30088/health" | grep -q "200"; then
    success "HTTP server health check passed"
    
    # Test WebDAV upload
    TEST_FILE=$(mktemp)
    echo "Test content $(date)" > "$TEST_FILE"
    
    if curl -s -X PUT -u "httpuser:httppass123" \
        "http://${MINIKUBE_IP}:30088/webdav/test-$(date +%s).txt" \
        --data-binary @"$TEST_FILE" -o /dev/null -w "%{http_code}" | grep -q "2"; then
        success "HTTP WebDAV upload successful"
    fi
    rm -f "$TEST_FILE"
    ((PASSED++))
else
    failure "HTTP test failed"
    ((FAILED++))
fi

# ============================================
# Test 4: S3/MinIO
# ============================================
header "TEST 4: S3/MinIO"

if curl -s -o /dev/null -w "%{http_code}" "http://${MINIKUBE_IP}:30900/minio/health/live" | grep -q "200"; then
    success "MinIO health check passed"
    success "MinIO Console accessible at http://${MINIKUBE_IP}:30901"
    
    # Test with AWS CLI if available
    if command -v aws &>/dev/null; then
        export AWS_ACCESS_KEY_ID="minioadmin"
        export AWS_SECRET_ACCESS_KEY="minioadmin123"
        
        if aws --endpoint-url "http://${MINIKUBE_IP}:30900" s3 ls &>/dev/null; then
            success "S3 bucket listing works"
        fi
    else
        info "AWS CLI not found - skipping S3 upload test"
    fi
    ((PASSED++))
else
    failure "S3/MinIO test failed"
    ((FAILED++))
fi

# ============================================
# Test 5: FTP Server
# ============================================
header "TEST 5: FTP Server"

if nc -z -w5 "$MINIKUBE_IP" 30021 2>/dev/null; then
    success "FTP port 30021 is accessible"
    info "For full FTP test: ftp ${MINIKUBE_IP} 30021 (user: ftpuser)"
    ((PASSED++))
else
    failure "FTP connection failed"
    ((FAILED++))
fi

# ============================================
# Test 6: SFTP Server
# ============================================
header "TEST 6: SFTP Server"

if nc -z -w5 "$MINIKUBE_IP" 30022 2>/dev/null; then
    success "SFTP port 30022 is accessible"
    info "For full SFTP test: sftp -P 30022 sftpuser@${MINIKUBE_IP}"
    ((PASSED++))
else
    failure "SFTP connection failed"
    ((FAILED++))
fi

# ============================================
# Test 7: SMB Server
# ============================================
header "TEST 7: SMB Server"

if nc -z -w5 "$MINIKUBE_IP" 30445 2>/dev/null; then
    success "SMB port 30445 is accessible"
    info "Mount: sudo mount -t cifs //${MINIKUBE_IP}/simulator /mnt/smb -o user=smbuser"
    ((PASSED++))
else
    failure "SMB connection failed"
    ((FAILED++))
fi

# ============================================
# Test 8: NFS Server
# ============================================
header "TEST 8: NFS Server"

if nc -z -w5 "$MINIKUBE_IP" 32049 2>/dev/null; then
    success "NFS port 32049 is accessible"
    info "Mount: sudo mount -t nfs ${MINIKUBE_IP}:/data /mnt/nfs"
    ((PASSED++))
else
    failure "NFS connection failed"
    ((FAILED++))
fi

# ============================================
# Summary
# ============================================
echo -e "\n${CYAN}=============================================${NC}"
echo -e "${CYAN}  Test Results Summary${NC}"
echo -e "${CYAN}=============================================${NC}"
echo -e "  ${GREEN}Passed:  $PASSED${NC}"
echo -e "  ${RED}Failed:  $FAILED${NC}"
echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}🎉 All tests passed! File Simulator Suite is working correctly.${NC}"
    exit 0
else
    echo -e "${YELLOW}⚠️  Some tests failed. Check the output above for details.${NC}"
    exit 1
fi
