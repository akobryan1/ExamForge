/** TanStack Query key factory — keeps cache keys consistent across the app */

export const examKeys = {
  all: ['exams'] as const,
  lists: () => [...examKeys.all, 'list'] as const,
  list: (filters?: string) => [...examKeys.lists(), filters].filter(Boolean) as readonly string[],
  details: () => [...examKeys.all, 'detail'] as const,
  detail: (id: string) => [...examKeys.details(), id] as const,
  questions: (examId: string) => [...examKeys.all, 'questions', examId] as const,
};

export const gradingKeys = {
  all: ['grading'] as const,
  queue: () => [...gradingKeys.all, 'queue'] as const,
};

export const analyticsKeys = {
  all: ['analytics'] as const,
  exam: (id: string) => [...analyticsKeys.all, id] as const,
};

export const incidentKeys = {
  all: ['incidents'] as const,
  list: () => [...incidentKeys.all, 'list'] as const,
};

export const studentKeys = {
  all: ['students'] as const,
  list: () => [...studentKeys.all, 'list'] as const,
};
