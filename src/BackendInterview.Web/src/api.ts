export type Topic = { id: string; title: string; position: number; url: string };
export type ContentBlock = {
  id: string;
  type: string;
  text: string;
  language?: string | null;
  checked?: boolean | null;
  url?: string | null;
  children: ContentBlock[];
};
export type TopicPage = { topic: Topic; content: ContentBlock[]; isComplete: boolean; retrievedAt: string };
export type Progress = {
  topics: number; readableTopics: number; completedItems: number; totalItems: number;
  percent: number | null; state: string;
};
export type Review = { id: string; topicId: string; topicTitle: string; note: string; date: string | null; url: string | null };
export type QuickQuestion = {
  id: string; title: string; standardQuestion: string; topic: string; category: string; difficulty: string;
  processingStatus: string; answerStatus: string; status: string; date: string | null; url: string;
};
export type QuickQuestionResults = { items: QuickQuestion[]; nextCursor: string | null; hasMore: boolean };
export type QuickQuestionDetail = { question: QuickQuestion; content: ContentBlock[]; isComplete: boolean; retrievedAt: string };

async function request<T>(path: string): Promise<T> {
  const response = await fetch(path);
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string; detail?: string } | null;
    throw new Error(problem?.detail ?? problem?.title ?? `Yêu cầu thất bại (${response.status})`);
  }
  return response.json() as Promise<T>;
}

export const api = {
  roadmap: () => request<Topic[]>('/api/roadmap'),
  topic: (id: string) => request<TopicPage>(`/api/topics/${encodeURIComponent(id)}`),
  progress: () => request<Progress>('/api/progress'),
  reviews: () => request<Review[]>('/api/reviews'),
  quickQuestions: (filters: { search?: string; category?: string; difficulty?: string; status?: string; cursor?: string }) => {
    const params = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => { if (value) params.set(key, value); });
    return request<QuickQuestionResults>(`/api/quick-questions?${params.toString()}`);
  },
  quickQuestion: (id: string) => request<QuickQuestionDetail>(`/api/quick-questions/${encodeURIComponent(id)}`),
};
