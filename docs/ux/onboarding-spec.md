# SNAPP Onboarding Wizard Specification

## Route

**`/onboarding`** — No application shell (no drawer, no nav). Minimal AppBar with logo only.

Displayed after first successful login when the user has no profile record (`PROFILE` item missing or `OnboardingComplete == false`).

---

## Layout

```
┌──────────────────────────────────────────────────────────────┐
│  MudAppBar (Flat, no elevation)                              │
│  [SNAPP Logo]                                    [Sign Out]  │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  MudContainer (MaxWidth.Small — 600px)                       │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  MudText (H1): "Let's set up your profile"            │  │
│  │  MudText (Body2): subtitle                            │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  MudStepper (Linear, Orientation.Horizontal)           │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐               │  │
│  │  │ About You│→│ Practice │→│ Connect  │               │  │
│  │  └──────────┘ └──────────┘ └──────────┘               │  │
│  │                                                        │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  Step Content Area                               │  │  │
│  │  │                                                  │  │  │
│  │  │  (form fields per step)                          │  │  │
│  │  │                                                  │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │                                                        │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │  [Back]                [Skip]        [Continue]  │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                              │
│  MudText (Caption): "You can update this anytime in         │
│  your profile settings."                                     │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

## Stepper Component

```razor
<MudStepper @bind-ActiveIndex="activeStep"
            Linear="true"
            Color="Color.Primary"
            Variant="Variant.Filled">
    <MudStep Title="About You" Icon="@Icons.Material.Filled.Person" />
    <MudStep Title="Your Practice" Icon="@Icons.Material.Filled.Business"
             Optional="true" />
    <MudStep Title="Connect" Icon="@Icons.Material.Filled.Link" />
</MudStepper>
```

---

## Step 1: About You

**Required step.** Minimum data to create a usable profile.

### Page Title
- **Heading**: "Tell us about yourself"
- **Subtitle**: "This helps us connect you with the right people and networks."

### Fields

| Field | Component | Props | Validation | Required |
|-------|-----------|-------|------------|----------|
| First name | `MudTextField` | `Label="First name" Variant="Variant.Outlined"` | Min 1 char, max 50 | Yes |
| Last name | `MudTextField` | `Label="Last name" Variant="Variant.Outlined"` | Min 1 char, max 50 | Yes |
| Display name | `MudTextField` | `Label="Display name" HelperText="How others will see you" Variant="Variant.Outlined"` | Auto-populated from first+last, editable. Max 100. | Yes |
| Specialty | `MudAutocomplete` | `Label="Primary specialty" Variant="Variant.Outlined" SearchFunc="SearchSpecialties" CoerceText="true"` | Must select from list or enter custom | Yes |
| State | `MudSelect` | `Label="State" Variant="Variant.Outlined"` | US states + territories | Yes |
| City / Metro area | `MudAutocomplete` | `Label="City or metro area" Variant="Variant.Outlined" SearchFunc="SearchCities"` | Free text, suggestions from geo database | Yes |

### Layout (within step)

```razor
<MudGrid Spacing="3">
    <MudItem xs="12" sm="6">
        <!-- First name -->
    </MudItem>
    <MudItem xs="12" sm="6">
        <!-- Last name -->
    </MudItem>
    <MudItem xs="12">
        <!-- Display name -->
    </MudItem>
    <MudItem xs="12">
        <!-- Specialty -->
    </MudItem>
    <MudItem xs="12" sm="6">
        <!-- State -->
    </MudItem>
    <MudItem xs="12" sm="6">
        <!-- City / Metro -->
    </MudItem>
</MudGrid>
```

### Actions

| Button | Position | Behavior |
|--------|----------|----------|
| Continue | Right | Validates all required fields. If valid, saves profile and advances to Step 2. |
| Back | Left | Disabled (first step). |

---

## Step 2: Your Practice (Optional)

**Optional step.** Adds practice context for intelligence scoring.

### Page Title
- **Heading**: "About your practice"
- **Subtitle**: "This unlocks benchmarking and practice intelligence. You can skip this for now."

### Fields

| Field | Component | Props | Validation | Required |
|-------|-----------|-------|------------|----------|
| Practice name | `MudTextField` | `Label="Practice name" Variant="Variant.Outlined"` | Max 200 | No |
| Role | `MudSelect` | `Label="Your role" Variant="Variant.Outlined"` | Options: Owner, Partner, Associate, Employee, Consultant, Other | No |
| Practice size | `MudSelect` | `Label="Practice size" HelperText="Approximate headcount" Variant="Variant.Outlined"` | Options: Solo, 2-5, 6-15, 16-50, 51-200, 200+ | No |
| Years in practice | `MudSelect` | `Label="Years in practice" Variant="Variant.Outlined"` | Options: < 1, 1-3, 4-7, 8-15, 16-25, 25+ | No |
| Practice type | `MudSelect` | `Label="Practice type" Variant="Variant.Outlined"` | Options: Solo private, Group private, DSO/MSO, Hospital/Institutional, Other | No |

### Layout

```razor
<MudGrid Spacing="3">
    <MudItem xs="12">
        <!-- Practice name -->
    </MudItem>
    <MudItem xs="12" sm="6">
        <!-- Role -->
    </MudItem>
    <MudItem xs="12" sm="6">
        <!-- Practice size -->
    </MudItem>
    <MudItem xs="12" sm="6">
        <!-- Years in practice -->
    </MudItem>
    <MudItem xs="12" sm="6">
        <!-- Practice type -->
    </MudItem>
</MudGrid>
```

### Actions

| Button | Position | Behavior |
|--------|----------|----------|
| Continue | Right | Saves any entered data and advances to Step 3. No validation required. |
| Skip | Center | Advances to Step 3 without saving. |
| Back | Left | Returns to Step 1 with data preserved. |

---

## Step 3: Connect

**Required step.** External identity connections.

### Page Title
- **Heading**: "Connect your accounts"
- **Subtitle**: "Link your LinkedIn profile to auto-fill your SNAPP profile and share milestones."

### Sections

#### LinkedIn Connection

```
┌────────────────────────────────────────────────────────────┐
│  [LinkedIn Logo]  Connect your LinkedIn profile            │
│                                                            │
│  We'll pull your name, photo, and headline                 │
│  so you don't have to type it again.                       │
│                                                            │
│  [Connect LinkedIn]                                        │
│                                                            │
│  Your LinkedIn credentials are never stored.               │
│  You can disconnect anytime.                               │
└────────────────────────────────────────────────────────────┘
```

Component structure:

```razor
<MudPaper Variant="Variant.Outlined" Class="pa-4 mb-4">
    <div class="d-flex align-center mb-3">
        <MudIcon Icon="@linkedInIcon" Size="Size.Large" Class="mr-3"
                 Style="color: #0A66C2;" />
        <MudText Typo="Typo.subtitle1">Connect your LinkedIn profile</MudText>
    </div>
    <MudText Typo="Typo.body2" Class="mb-3">
        We'll pull your name, photo, and headline so you don't have to type it again.
    </MudText>
    @if (!linkedInConnected)
    {
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   StartIcon="@linkedInIcon"
                   OnClick="ConnectLinkedIn">
            Connect LinkedIn
        </MudButton>
    }
    else
    {
        <MudAlert Severity="Severity.Success" Dense="true">
            Connected as @linkedInName
        </MudAlert>
    }
    <MudText Typo="Typo.caption" Class="mt-2" Color="Color.Secondary">
        Your LinkedIn credentials are never stored. You can disconnect anytime.
    </MudText>
</MudPaper>
```

#### Phone Number (Optional)

```razor
<MudPaper Variant="Variant.Outlined" Class="pa-4">
    <MudText Typo="Typo.subtitle1" Class="mb-2">Phone number</MudText>
    <MudText Typo="Typo.body2" Class="mb-3">
        Optional. Used only for account recovery — never shared or displayed.
    </MudText>
    <MudTextField @bind-Value="phone"
                  Label="Phone number"
                  Variant="Variant.Outlined"
                  InputType="InputType.Telephone"
                  Mask="@(new PatternMask("(000) 000-0000"))"
                  HelperText="US phone number" />
</MudPaper>
```

### Actions

| Button | Position | Behavior |
|--------|----------|----------|
| Finish | Right | Saves any data, marks `OnboardingComplete = true`, redirects to `/feed`. Label: **"Get Started"** |
| Skip LinkedIn | Center | Only visible if LinkedIn not connected. Advances to completion without LinkedIn. |
| Back | Left | Returns to Step 2 with data preserved. |

---

## Microcopy

### Page-Level

| Location | Text |
|----------|------|
| Page heading | "Let's set up your profile" |
| Page subtitle | "Takes about 2 minutes. You can update everything later." |
| Bottom helper | "You can update this anytime in your profile settings." |

### Step 1

| Location | Text |
|----------|------|
| Step label | "About You" |
| Step heading | "Tell us about yourself" |
| Step subtitle | "This helps us connect you with the right people and networks." |
| Display name helper | "How others will see you" |

### Step 2

| Location | Text |
|----------|------|
| Step label | "Your Practice" |
| Step heading | "About your practice" |
| Step subtitle | "This unlocks benchmarking and practice intelligence. You can skip this for now." |
| Practice size helper | "Approximate headcount" |

### Step 3

| Location | Text |
|----------|------|
| Step label | "Connect" |
| Step heading | "Connect your accounts" |
| Step subtitle | "Link your LinkedIn profile to auto-fill your SNAPP profile and share milestones." |
| LinkedIn body | "We'll pull your name, photo, and headline so you don't have to type it again." |
| LinkedIn trust | "Your LinkedIn credentials are never stored. You can disconnect anytime." |
| Phone body | "Optional. Used only for account recovery — never shared or displayed." |
| Phone helper | "US phone number" |
| Finish button | "Get Started" |

### Validation Messages

| Field | Error |
|-------|-------|
| First name (empty) | "First name is required" |
| Last name (empty) | "Last name is required" |
| Display name (empty) | "Display name is required" |
| Specialty (empty) | "Please select or enter your specialty" |
| State (empty) | "Please select your state" |
| City (empty) | "Please enter your city or metro area" |
| Phone (invalid) | "Please enter a valid US phone number" |

---

## States

### Loading (Initial)

While checking if user needs onboarding:

```razor
<MudContainer MaxWidth="MaxWidth.Small" Class="d-flex justify-center" Style="min-height: 50vh;">
    <MudProgressCircular Color="Color.Primary" Indeterminate="true" />
</MudContainer>
```

### Saving (Between Steps)

Disable the active action button and show a spinner:

```razor
<MudButton Variant="Variant.Filled" Color="Color.Primary"
           Disabled="@saving" OnClick="NextStep">
    @if (saving)
    {
        <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
    }
    Continue
</MudButton>
```

### Error (Save Failed)

Show an alert above the step content:

```razor
<MudAlert Severity="Severity.Error" Dense="true" Class="mb-4"
          CloseIconClicked="DismissError">
    We couldn't save your information. Please try again.
</MudAlert>
```

### LinkedIn OAuth Error

If LinkedIn connection fails:

```razor
<MudAlert Severity="Severity.Warning" Dense="true" Class="mb-3">
    LinkedIn connection failed. You can try again or skip for now.
</MudAlert>
```

### LinkedIn Already Connected

Show success state (green alert with name) and change button to "Disconnect".

---

## Responsive Behavior

### 375px (Mobile)

- `MudContainer MaxWidth.Small` shrinks to full-width with `pa-3`
- Stepper switches to `Orientation.Vertical` with compact labels
- All form fields stack to full width (`xs="12"`)
- Action buttons stack vertically: Continue full-width, Back/Skip below as text buttons

### 768px (Tablet)

- Container centered at 600px
- Stepper remains horizontal
- Name fields side-by-side (`sm="6"`)
- Action buttons in a single row

### 1440px (Desktop)

- Container centered at 600px (intentionally narrow for focus)
- Same layout as tablet — onboarding is a focused single-column experience

---

## Post-Onboarding

On "Get Started" click:
1. Save Step 3 data (phone, LinkedIn)
2. Set `OnboardingComplete = true` on user profile
3. Navigate to `/feed`
4. Show a `MudSnackbar`:

```
"Welcome to SNAPP! Explore networks or create your own."
```

Configuration: `Severity.Normal`, `SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight`, auto-hide after 5 seconds.
