# Publishing MDTool to NuGet

This guide explains how to publish MDTool to NuGet.org using GitHub Actions.

## Prerequisites

### 1. Create a NuGet Account

1. Go to https://www.nuget.org
2. Click "Sign in" (top right)
3. Sign in with one of:
   - Microsoft account
   - GitHub account (recommended)
   - Email

### 2. Generate NuGet API Key

1. Once logged in, click your username (top right)
2. Select **API Keys**
3. Click **Create**
4. Configure the API key:
   - **Key Name:** `MDTool-GitHub-Actions`
   - **Expiration:** 365 days (or choose your preference)
   - **Scopes:**
     - ✅ Push
     - ✅ Push new packages and package versions
   - **Glob Pattern:** `Rand.MDTool*`
     - This restricts the key to only publish packages starting with "Rand.MDTool"
     - Provides security in case the key is compromised
5. Click **Create**
6. **⚠️ IMPORTANT:** Copy the API key immediately (it's shown only once!)

### 3. Add API Key to GitHub Secrets

1. Go to your GitHub repository: https://github.com/randlee/mdtool
2. Click **Settings** (tab at top)
3. In the left sidebar, click **Secrets and variables** → **Actions**
4. Click **New repository secret**
5. Configure the secret:
   - **Name:** `NUGET_API_KEY` (must match exactly)
   - **Value:** Paste the API key you copied from NuGet
6. Click **Add secret**

---

## Publishing Workflow

The repository includes an automated publishing workflow at `.github/workflows/publish.yml`.

### Automatic Publishing (Recommended)

The workflow automatically publishes to NuGet when you create a GitHub release:

#### Step 1: Update Version (if needed)

Edit `src/MDTool/MDTool.csproj` and update the version:

```xml
<Version>1.0.0</Version>  <!-- Change to 1.1.0, 2.0.0, etc. -->
```

Commit and push the change.

#### Step 2: Create a GitHub Release

**Option A: Using GitHub CLI**
```bash
# Tag the current commit
git tag v1.0.0
git push origin v1.0.0

# Create release
gh release create v1.0.0 \
  --title "v1.0.0 - Initial Release" \
  --notes "First public release of MDTool

Features:
- Variable substitution with YAML frontmatter
- Conditional sections support
- 4 CLI commands: get-schema, validate, process, generate-header
- Cross-platform support (Windows, macOS, Linux)

Install:
\`\`\`bash
dotnet tool install --global Rand.MDTool
\`\`\`"
```

**Option B: Using GitHub Web UI**
1. Go to https://github.com/randlee/mdtool/releases
2. Click **Draft a new release**
3. Click **Choose a tag** → Type `v1.0.0` → Click **Create new tag**
4. **Release title:** `v1.0.0 - Initial Release`
5. **Description:** Add release notes (features, changes, install instructions)
6. Click **Publish release**

#### Step 3: Monitor the Workflow

1. Go to https://github.com/randlee/mdtool/actions
2. Click on the "Publish to NuGet" workflow
3. Watch the progress:
   - ✅ Build
   - ✅ Test (414 tests)
   - ✅ Create Package
   - ✅ Publish to NuGet
   - ✅ Upload Artifact

#### Step 4: Verify on NuGet

1. Wait 5-10 minutes for NuGet validation
2. Check your package: https://www.nuget.org/packages/Rand.MDTool
3. Verify the version number is correct
4. Test installation:
   ```bash
   dotnet tool install --global Rand.MDTool
   mdtool --version
   ```

---

### Manual Publishing (Alternative)

You can also manually trigger the workflow without creating a release:

1. Go to https://github.com/randlee/mdtool/actions
2. Click **Publish to NuGet** (left sidebar)
3. Click **Run workflow** (right side)
4. Click the green **Run workflow** button

---

## Publishing Checklist

Before publishing a new version:

- [ ] Update version in `src/MDTool/MDTool.csproj`
- [ ] Update CHANGELOG.md with version notes
- [ ] Run tests locally: `dotnet test --configuration Release`
- [ ] Verify all 414 tests pass
- [ ] Update README.md if new features added
- [ ] Commit and push all changes
- [ ] Create GitHub release with version tag
- [ ] Monitor GitHub Actions workflow
- [ ] Verify package on NuGet.org
- [ ] Test installation: `dotnet tool install --global Rand.MDTool`
- [ ] Verify badges update in README

---

## Version Numbering

Follow [Semantic Versioning](https://semver.org/):

- **MAJOR** (1.x.x → 2.0.0): Breaking changes
- **MINOR** (1.0.x → 1.1.0): New features (backward compatible)
- **PATCH** (1.0.0 → 1.0.1): Bug fixes

Current version: **1.0.0**

Planned versions:
- **1.1.0**: Phase 3 - Macro System (env vars, file expansion)
- **1.2.0**: Phase 4 - Advanced Features (loops, functions)

---

## Troubleshooting

### "The specified API key is invalid"

- Verify the API key is correctly added to GitHub Secrets as `NUGET_API_KEY`
- Check the key hasn't expired
- Regenerate the API key on NuGet.org if needed

### "Package already exists"

- This means a package with this version number is already published
- Update the version number in `MDTool.csproj`
- NuGet doesn't allow re-publishing the same version

### "Tests failed"

- The workflow won't publish if tests fail
- Fix the failing tests locally
- Push the fix and re-run the workflow

### "Package not appearing on NuGet"

- Wait 5-10 minutes for validation
- Wait 15-30 minutes for search indexing
- Direct package link works immediately: https://www.nuget.org/packages/Rand.MDTool

---

## Security Notes

- **Never commit API keys** to the repository
- **Always use GitHub Secrets** for sensitive credentials
- **Use scoped API keys** (glob pattern `Rand.MDTool*`)
- **Set expiration dates** on API keys
- **Rotate keys regularly** (recommended: yearly)

---

## Manual Publishing (Local)

If you need to publish manually from your local machine:

```bash
# Build and test
dotnet build --configuration Release
dotnet test --configuration Release --no-build

# Create package
dotnet pack src/MDTool/MDTool.csproj --configuration Release --output ./nupkg

# Publish to NuGet
dotnet nuget push ./nupkg/Rand.MDTool.*.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key <YOUR_API_KEY>
```

**Note:** GitHub Actions is the recommended approach for consistency and auditability.

---

## Support

- **NuGet Package:** https://www.nuget.org/packages/Rand.MDTool
- **GitHub Repository:** https://github.com/randlee/mdtool
- **Issues:** https://github.com/randlee/mdtool/issues
