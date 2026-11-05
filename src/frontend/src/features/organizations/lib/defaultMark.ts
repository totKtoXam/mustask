import type { DefaultMark } from '../api/organizationsApi';

/**
 * The mark a team carries for the project's default (mod-017 design § 7): text,
 * never colour alone. One team carries it at most; two teams and no setting:
 * none.
 */
export const DEFAULT_MARK_LABEL: Readonly<Record<DefaultMark, string>> = {
  named: 'По умолчанию',
  onlyTeam: 'По умолчанию: единственная команда',
};
