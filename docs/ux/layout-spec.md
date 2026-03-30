# SNAPP Application Layout Specification

## Route Structure

| Route Pattern | Page |
|---------------|------|
| `/` | Redirect to `/feed` (authenticated) or `/login` (unauthenticated) |
| `/login` | Login page (no shell) |
| `/login/check-email` | Check email confirmation (no shell) |
| `/login/callback?code=...` | Magic link callback (no shell) |
| `/onboarding` | Onboarding wizard (no shell) |
| `/feed` | Network feed (default home) |
| `/profile/{userId?}` | Profile view/edit |
| `/network/{netId}` | Network dashboard |
| `/network/{netId}/members` | Member directory |
| `/network/{netId}/discussions` | Discussion threads |
| `/network/{netId}/referrals` | Referral board |
| `/network/{netId}/deals` | Deal room list |
| `/network/{netId}/settings` | Network settings (stewards) |
| `/intelligence` | Practice intelligence dashboard |
| `/intelligence/benchmarks` | Benchmark explorer |
| `/intelligence/valuation` | Valuation estimate |
| `/notifications` | Notification history |
| `/settings` | User settings, preferences |

---

## Shell Layout (Authenticated Pages)

All authenticated pages share this shell. Login, callback, and onboarding pages render without the shell.

```
┌──────────────────────────────────────────────────────────────────┐
│  MudAppBar (Fixed, Dense, Elevation=1)                          │
│  [☰] [SNAPP Logo]           [🔍 Search] [🔔 Bell] [👤 Avatar] │
├──────────────┬───────────────────────────────────────────────────┤
│  MudDrawer   │  MudMainContent                                  │
│  (280px)     │                                                   │
│              │  ┌────────────────────────────────────────────┐   │
│  Network     │  │  MudContainer (MaxWidth.Large)             │   │
│  Selector    │  │                                            │   │
│              │  │  Page content renders here                 │   │
│  ── divider  │  │                                            │   │
│              │  │                                            │   │
│  Nav Links   │  │                                            │   │
│  (context-   │  │                                            │   │
│   sensitive) │  │                                            │   │
│              │  └────────────────────────────────────────────┘   │
│  ── divider  │                                                   │
│              │                                                   │
│  [+ Create   │                                                   │
│   Network]   │                                                   │
│              │                                                   │
│  ── spacer   │                                                   │
│  [Settings]  │                                                   │
│  [Help]      │                                                   │
└──────────────┴───────────────────────────────────────────────────┘
```

---

## AppBar Specification

### Component

```razor
<MudAppBar Fixed="true" Dense="true" Elevation="1"
           Color="Color.Surface" Style="border-bottom: 1px solid var(--mud-palette-divider);">
```

### Left Section

| Element | Component | Props | Behavior |
|---------|-----------|-------|----------|
| Hamburger menu | `MudIconButton` | `Icon="@Icons.Material.Filled.Menu"` | Toggles drawer on all breakpoints |
| Logo | `MudImage` | `Src="/images/snapp-logo.svg" Height="28"` | Navigates to `/feed` on click |
| Logo text | `MudText` | `Typo="Typo.h6" Class="ml-2"` | Text: **SNAPP** — hidden below 768px |

### Right Section

| Element | Component | Props | Behavior |
|---------|-----------|-------|----------|
| Search | `MudIconButton` | `Icon="@Icons.Material.Filled.Search"` | Opens `MudOverlay` with `MudTextField` (Adornment search icon). Searches members, networks, discussions. |
| Notifications | `MudBadge` wrapping `MudIconButton` | `BadgeContent="@unreadCount" Color="Color.Error" Overlap="true" Visible="@(unreadCount > 0)"` | Bell icon. Clicking opens `MudPopover` with notification list. Badge shows unread count (max display: "9+"). |
| Profile | `MudMenu` wrapping `MudAvatar` | `AnchorOrigin="Origin.BottomRight"` | Avatar shows user initials or photo. Menu items: **My Profile**, **Settings**, divider, **Sign Out**. |

### Notification Popover Content

```razor
<MudPopover Open="@notifOpen" AnchorOrigin="Origin.BottomRight">
    <MudPaper Elevation="4" Style="width: 360px; max-height: 480px;">
        <MudToolBar Dense="true">
            <MudText Typo="Typo.subtitle1">Notifications</MudText>
            <MudSpacer />
            <MudButton Variant="Variant.Text" Size="Size.Small">Mark all read</MudButton>
        </MudToolBar>
        <MudDivider />
        <MudList T="NotificationDto" Dense="true">
            <!-- NotificationItem for each notification -->
        </MudList>
        <MudDivider />
        <MudButton FullWidth="true" Variant="Variant.Text" Href="/notifications">
            View all notifications
        </MudButton>
    </MudPaper>
</MudPopover>
```

### Search Overlay

When search is activated, a full-width overlay appears below the AppBar:

```razor
<MudOverlay Visible="@searchOpen" OnClick="CloseSearch" DarkBackground="true">
    <MudPaper Elevation="4" Class="pa-4" Style="width: 600px; margin: 80px auto 0;">
        <MudTextField @bind-Value="searchQuery"
                      Placeholder="Search members, networks, discussions..."
                      Variant="Variant.Outlined"
                      Adornment="Adornment.Start"
                      AdornmentIcon="@Icons.Material.Filled.Search"
                      Immediate="true"
                      AutoFocus="true" />
        <!-- Results appear below as MudList after 2+ characters typed -->
        <!-- Grouped by: Members, Networks, Discussions -->
        <!-- Max 3 results per group, "See all" link per group -->
    </MudPaper>
</MudOverlay>
```

---

## Drawer Specification

### Component

```razor
<MudDrawer @bind-Open="drawerOpen"
           ClipMode="DrawerClipMode.Always"
           Variant="@drawerVariant"
           Elevation="0"
           Width="280px"
           Style="border-right: 1px solid var(--mud-palette-divider);">
```

- **Desktop (>=1024px)**: `DrawerVariant.Mini` — persistent, collapsible to icons
- **Tablet (768-1023px)**: `DrawerVariant.Responsive` — overlay, hidden by default
- **Mobile (<768px)**: `DrawerVariant.Temporary` — overlay with backdrop

### Network Selector

At the top of the drawer. Lets users switch active network context.

```razor
<MudSelect T="string" @bind-Value="activeNetworkId"
           Label="Network"
           Variant="Variant.Filled"
           Class="mx-3 mt-3"
           Dense="true"
           AnchorOrigin="Origin.BottomCenter">
    <MudSelectItem Value="@("all")">
        <MudIcon Icon="@Icons.Material.Filled.Public" Class="mr-2" Size="Size.Small" />
        All Networks
    </MudSelectItem>
    @foreach (var net in userNetworks)
    {
        <MudSelectItem Value="@net.NetworkId">
            <MudAvatar Size="Size.Small" Class="mr-2"
                       Style="background-color: @net.Color;">
                @net.Name[0]
            </MudAvatar>
            @net.Name
        </MudSelectItem>
    }
</MudSelect>
```

### Navigation Links

Context-sensitive based on selected network. Two modes:

#### Global Mode (All Networks selected)

```
📰  Feed
👤  My Profile
📊  Practice Intelligence
🔔  Notifications
```

#### Network Mode (Specific network selected)

```
📰  Feed
💬  Discussions
🤝  Referrals
👥  Members
📊  Intelligence
🔒  Deal Room          (if user has deal_room permission)
⚙️  Network Settings   (if user has steward role)
```

### Navigation Component

```razor
<MudNavMenu Dense="true" Class="mt-2">
    <MudNavLink Href="/feed"
                Icon="@Icons.Material.Filled.DynamicFeed"
                Match="NavLinkMatch.Prefix">
        Feed
    </MudNavLink>
    <!-- Additional nav items per mode above -->
</MudNavMenu>
```

### Create Network Button

Below the nav links, separated by a divider:

```razor
<MudDivider Class="my-2" />
<MudButton Variant="Variant.Outlined"
           Color="Color.Primary"
           StartIcon="@Icons.Material.Filled.Add"
           FullWidth="true"
           Class="mx-3"
           Href="/network/create">
    Create Network
</MudButton>
```

### Footer Links

Pinned to drawer bottom:

```razor
<MudSpacer />
<MudDivider />
<MudNavMenu Dense="true">
    <MudNavLink Href="/settings" Icon="@Icons.Material.Filled.Settings">
        Settings
    </MudNavLink>
    <MudNavLink Href="https://help.snapp.praxisiq.com"
                Icon="@Icons.Material.Filled.HelpOutline"
                Target="_blank">
        Help
    </MudNavLink>
</MudNavMenu>
```

---

## MainContent Area

```razor
<MudMainContent Class="pt-16">
    <MudContainer MaxWidth="MaxWidth.Large" Class="pa-4 pa-md-6">
        @Body
    </MudContainer>
</MudMainContent>
```

- `pt-16` compensates for fixed AppBar height
- `MaxWidth.Large` = 1280px max content width
- Page components render into `@Body`

---

## Responsive Behavior

### 375px (Mobile)

| Element | Behavior |
|---------|----------|
| AppBar | Logo text hidden. Search, bell, avatar remain. Hamburger visible. |
| Drawer | `DrawerVariant.Temporary` — hidden, opens as full-width overlay with backdrop on hamburger tap. |
| Network selector | Full-width within drawer overlay. |
| MainContent | `pa-3` padding. Single-column layout. Cards stack vertically. |
| Search | Overlay expands to full screen width minus 16px margin. |
| Notification popover | Full-screen-width sheet sliding up from bottom (`MudDrawer Anchor="Anchor.Bottom"`). |
| Tables | Horizontal scroll or collapse to card view. |
| Grid layouts | All items 12-col (`xs="12"`). |

### 768px (Tablet)

| Element | Behavior |
|---------|----------|
| AppBar | Logo text visible. All icons visible. |
| Drawer | `DrawerVariant.Responsive` — hidden by default, opens as overlay on hamburger tap. |
| MainContent | `pa-4` padding. Two-column layouts possible. |
| Search | 600px max-width centered overlay. |
| Notification popover | 360px popover anchored to bell icon. |
| Grid layouts | Two-col where useful (`sm="6"`), single-col for forms. |

### 1440px (Desktop)

| Element | Behavior |
|---------|----------|
| AppBar | Full display. |
| Drawer | `DrawerVariant.Mini` — persistent, expanded by default. Can be collapsed to 60px icon rail. |
| MainContent | `pa-6` padding. Full multi-column layouts. MaxWidth 1280px centered. |
| Search | 600px centered overlay. |
| Notification popover | 360px popover anchored to bell icon. |
| Grid layouts | Three or four-col dashboards (`md="4"`, `lg="3"`). |

---

## Layout States

### Loading (Initial App Load)

Before authentication state is resolved:

```razor
<MudLayout>
    <MudAppBar Fixed="true" Dense="true" Elevation="1" Color="Color.Surface">
        <MudImage Src="/images/snapp-logo.svg" Height="28" />
        <MudText Typo="Typo.h6" Class="ml-2">SNAPP</MudText>
    </MudAppBar>
    <MudMainContent Class="pt-16">
        <MudContainer MaxWidth="MaxWidth.Small" Class="d-flex justify-center align-center"
                      Style="min-height: 60vh;">
            <MudProgressCircular Color="Color.Primary" Indeterminate="true" Size="Size.Large" />
        </MudContainer>
    </MudMainContent>
</MudLayout>
```

### Unauthenticated

No drawer. Redirect to `/login`. AppBar shows only logo (no search, bell, or avatar).

### No Networks

User is authenticated but has not joined any networks:

- Drawer shows "All Networks" with no other options
- MainContent shows an empty state card:

```
┌──────────────────────────────────────────┐
│  [illustration: connected nodes]         │
│                                          │
│  Welcome to SNAPP                        │
│  Join a network to connect with          │
│  fellow practitioners.                   │
│                                          │
│  [Browse Networks]  [Create a Network]   │
└──────────────────────────────────────────┘
```

### Error (Shell-Level)

If a service call fails at the layout level (e.g., loading user networks):

```razor
<MudAlert Severity="Severity.Error" Dense="true" Class="ma-4">
    Something went wrong loading your networks.
    <MudLink OnClick="Retry">Try again</MudLink>
</MudAlert>
```

Drawer shows last-cached network list if available, or the empty state above.
