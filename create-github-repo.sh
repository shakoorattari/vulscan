#!/bin/bash
# GitHub Repository Setup Script for Vulscan

set -e

REPO_NAME="vulscan"
REPO_DESCRIPTION="🛡️ Vulscan — Enterprise Vulnerability Scanning Platform for Azure DevOps | .NET 10 + Angular 19 | SBOM Generation & CVE Detection"
GITHUB_USERNAME=""

echo "=================================================="
echo "  Vulscan — GitHub Repository Creator"
echo "=================================================="
echo ""

# Check if GitHub token is set
if [ -z "$GITHUB_TOKEN" ]; then
    echo "❌ Error: GITHUB_TOKEN environment variable is not set"
    echo ""
    echo "To create a GitHub repository via API, you need a Personal Access Token."
    echo ""
    echo "Steps to create a token:"
    echo "1. Go to: https://github.com/settings/tokens/new"
    echo "2. Give it a name: 'Vulscan Repository Creation'"
    echo "3. Select scopes: 'repo' (Full control of private repositories)"
    echo "4. Click 'Generate token'"
    echo "5. Copy the token and run:"
    echo "   export GITHUB_TOKEN='your_token_here'"
    echo ""
    echo "Then run this script again."
    echo ""
    echo "=================================================="
    echo "Alternative: Manual Creation via Web Browser"
    echo "=================================================="
    echo ""
    echo "1. Go to: https://github.com/new"
    echo "2. Repository name: $REPO_NAME"
    echo "3. Description: $REPO_DESCRIPTION"
    echo "4. Make it: Public ✅"
    echo "5. Do NOT initialize with README (we already have one)"
    echo "6. Click 'Create repository'"
    echo ""
    echo "Then run these commands to push your code:"
    echo ""
    echo "  git remote add origin https://github.com/YOUR_USERNAME/$REPO_NAME.git"
    echo "  git push -u origin main"
    echo ""
    exit 1
fi

# Get GitHub username
echo "Enter your GitHub username:"
read -r GITHUB_USERNAME

if [ -z "$GITHUB_USERNAME" ]; then
    echo "❌ Error: GitHub username is required"
    exit 1
fi

echo ""
echo "Creating repository: $GITHUB_USERNAME/$REPO_NAME"
echo "Description: $REPO_DESCRIPTION"
echo ""

# Create the repository using GitHub API
RESPONSE=$(curl -s -X POST \
    -H "Authorization: token $GITHUB_TOKEN" \
    -H "Accept: application/vnd.github+json" \
    https://api.github.com/user/repos \
    -d "{
        \"name\": \"$REPO_NAME\",
        \"description\": \"$REPO_DESCRIPTION\",
        \"private\": false,
        \"has_issues\": true,
        \"has_projects\": true,
        \"has_wiki\": true,
        \"auto_init\": false
    }")

# Check if creation was successful
if echo "$RESPONSE" | grep -q '"html_url"'; then
    REPO_URL=$(echo "$RESPONSE" | grep -o '"html_url": "[^"]*' | sed 's/"html_url": "//')
    echo "✅ Repository created successfully!"
    echo ""
    echo "Repository URL: $REPO_URL"
    echo ""
    
    # Add remote and push
    echo "Adding remote origin..."
    git remote add origin "https://github.com/$GITHUB_USERNAME/$REPO_NAME.git"
    
    echo "Pushing to GitHub..."
    git push -u origin main
    
    echo ""
    echo "=================================================="
    echo "✅ SUCCESS! Your code is now on GitHub!"
    echo "=================================================="
    echo ""
    echo "Repository: $REPO_URL"
    echo ""
else
    echo "❌ Error creating repository"
    echo ""
    echo "Response from GitHub:"
    echo "$RESPONSE" | python3 -m json.tool 2>/dev/null || echo "$RESPONSE"
    echo ""
    echo "Please check:"
    echo "- Your GitHub token is valid"
    echo "- The repository name doesn't already exist"
    echo "- Your token has 'repo' scope"
    exit 1
fi
