# Installing ECS/DOTS Packages for Unity 2021.3

## Manual Installation Steps

**Unity 2021.3** requires installing ECS packages manually via Package Manager:

### Step 1: Open Package Manager
- **Window → Package Manager**

### Step 2: Switch to Unity Registry
- Top-left dropdown: Change from "In Project" to **"Unity Registry"**

### Step 3: Install Packages (in this order)

1. **Entities** (Core ECS)
   - Search: "Entities"
   - Install: `com.unity.entities` (version 0.17.x or latest compatible)

2. **Collections**
   - Search: "Collections"
   - Install: `com.unity.collections`

3. **Jobs**
   - Search: "Jobs"
   - Install: `com.unity.jobs`

4. **Burst**
   - Search: "Burst"
   - Install: `com.unity.burst` (should already be installed)

5. **Mathematics**
   - Search: "Mathematics"
   - Install: `com.unity.mathematics`

### Step 4: Verify Installation
- Check Console for errors
- Packages should appear in Package Manager under "In Project"

### Alternative: Use Package Manager UI
- Click **"+"** button in Package Manager
- Select **"Add package by name..."**
- Enter package names one by one:
  - `com.unity.entities`
  - `com.unity.collections`
  - `com.unity.jobs`
  - `com.unity.burst`
  - `com.unity.mathematics`

## After Installation

Once packages are installed, the ECS scripts should compile. If you still see errors, let me know and we'll fix the code to match the installed package versions.
