import { Navigate, Route, Routes } from 'react-router-dom';
import { labels } from './labels';
import { GridEditor } from './features/config/GridEditor';
import { GridsList } from './features/config/GridsList';
import { ScaleEditor } from './features/config/ScaleEditor';
import { ScalesList } from './features/config/ScalesList';
import { Landing } from './portals/DevRoleSwitcher';
import { AdminCycles } from './features/assessment/AdminCycles';
import { AdminBoard, ManagerBoard } from './features/assessment/BoardPages';
import { ManagerAssessments } from './features/assessment/ManagerAssessments';
import { CareerCentre } from './features/career/CareerCentre';
import { MyIdp, TeamIdps } from './portals/Pages';
import { PortalLayout } from './portals/PortalLayout';

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Landing />} />
      <Route path="/employee" element={<PortalLayout role="employee" links={[{ to: '/employee', label: labels.nav.career }, { to: '/employee/idp', label: labels.nav.idp }]} />}>
        <Route index element={<CareerCentre />} />
        <Route path="idp" element={<MyIdp />} />
      </Route>
      <Route path="/manager" element={<PortalLayout role="manager" links={[{ to: '/manager', label: labels.nav.assessments }, { to: '/manager/board', label: labels.nav.teamBoard }, { to: '/manager/idps', label: labels.nav.teamIdps }]} />}>
        <Route index element={<ManagerAssessments />} />
        <Route path="board" element={<ManagerBoard />} />
        <Route path="idps" element={<TeamIdps />} />
      </Route>
      <Route path="/admin" element={<PortalLayout role="hr-admin" links={[{ to: '/admin/scales', label: labels.nav.scales }, { to: '/admin/grids', label: labels.nav.grids }, { to: '/admin/cycles', label: labels.nav.cycles }]} />}>
        <Route index element={<Navigate to="scales" replace />} />
        <Route path="scales" element={<ScalesList />} />
        <Route path="scales/:id" element={<ScaleEditor />} />
        <Route path="grids" element={<GridsList />} />
        <Route path="grids/:id" element={<GridEditor />} />
        <Route path="cycles" element={<AdminCycles />} />
        <Route path="cycles/:id/board" element={<AdminBoard />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
