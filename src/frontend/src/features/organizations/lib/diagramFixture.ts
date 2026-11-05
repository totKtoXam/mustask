// A small organization for the diagram's pure tests (feat-049): two departments, a top-level
// project, a deactivated project and teams that stand in one, two or no department.
import type { OfferedProject, OfferedTeam, SelectorOffers } from '../api/navigationApi';
import type { AreaNode } from '../api/organizationsApi';

function area(
  id: string,
  parentId: string | null,
  type: AreaNode['type'],
  name: string,
  projectId: string | null = null,
  projectActive: boolean | null = null,
): AreaNode {
  return {
    id,
    parentId,
    type,
    projectId,
    projectActive: type === 'department' ? null : (projectActive ?? true),
    name,
    description: null,
    path: name,
    depth: 1,
  };
}

export const diagramAreas: AreaNode[] = [
  area('d1', null, 'department', 'Логистика'),
  area('nA', 'd1', 'project', 'Склад', 'pA'),
  area('a1', 'nA', 'area', 'Приёмка', 'pA'),
  area('a2', 'a1', 'area', 'Ячейки', 'pA'),
  area('d2', null, 'department', 'Финансы'),
  area('nB', 'd2', 'project', 'Биллинг', 'pB'),
  area('nC', 'd2', 'project', 'Архив', 'pC', false),
  area('nF', null, 'project', 'Мобильное', 'pF'),
];

const project = (id: string, key: string, teamIds: string[]): OfferedProject => ({
  id,
  name: id,
  key,
  departmentId: null,
  departmentName: null,
  teamIds,
});
const team = (id: string, departmentIds: string[]): OfferedTeam => ({
  id,
  name: id,
  isMember: false,
  departmentIds,
});

export const diagramOffers: SelectorOffers = {
  departments: [],
  projects: [
    project('pA', 'WH', ['MARS', 'VENUS']),
    project('pB', 'BIL', ['MARS', 'SATURN']),
    project('pF', 'MOB', ['JUPITER']),
  ],
  teams: [
    team('ATLAS', []),
    team('JUPITER', ['d1', 'd2']),
    team('MARS', ['d1']),
    team('SATURN', ['d2']),
    team('VENUS', ['d1', 'missing']),
  ],
};
