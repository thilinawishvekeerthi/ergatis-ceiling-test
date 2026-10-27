/** All user-facing text lives here (English only for now). */
export const labels = {
  appName: 'Talent',
  portals: {
    employee: { title: 'My career', home: 'Career Centre' },
    manager: { title: 'My team', home: 'Assessments' },
    'hr-admin': { title: 'HR administration', home: 'Configuration' },
  },
  nav: { scales: 'Rating scales', grids: 'Grids', idp: 'Development plan', teamIdps: 'Team plans', assessments: 'Assessments', career: 'Career Centre' },
  noAccess: 'You do not have access to this area. Ask your HR administrator if you need it.',
  conflict: 'Someone else changed this record while you were editing. Reload to see their version.',
  reload: 'Reload',
  save: 'Save draft',
  saved: 'Draft saved',
  loading: 'Loading',
  loadFailed: 'Could not load this page. Try again.',
  emptyScales: 'No rating scales yet.',
  emptyGrids: 'No grids yet.',
  switchRole: 'Signed in as',
} as const;
