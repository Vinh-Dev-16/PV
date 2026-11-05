import { useEffect, useMemo, useState } from 'react';
import { api, type ContentBlock, type Progress, type QuickQuestion, type QuickQuestionDetail, type QuickQuestionResults, type Review, type Topic, type TopicPage } from './api';

type View = 'roadmap' | 'topic' | 'reviews' | 'quick-questions';

function App() {
  const [topics, setTopics] = useState<Topic[]>([]);
  const [selected, setSelected] = useState<Topic | null>(null);
  const [page, setPage] = useState<TopicPage | null>(null);
  const [progress, setProgress] = useState<Progress | null>(null);
  const [reviews, setReviews] = useState<Review[]>([]);
  const [view, setView] = useState<View>('roadmap');
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState('');
  const [query, setQuery] = useState('');
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    Promise.all([api.roadmap(), api.progress()])
      .then(([roadmap, currentProgress]) => {
        setTopics(roadmap); setProgress(currentProgress);
      })
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Không tải được dữ liệu.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (view !== 'reviews') return;
    setLoading(true);
    api.reviews()
      .then(setReviews)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Không tải được review.'))
      .finally(() => setLoading(false));
  }, [view]);

  async function openTopic(topic: Topic) {
    setSelected(topic); setView('topic'); setPage(null); setError(''); setDetailLoading(true);
    try { setPage(await api.topic(topic.id)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Không đọc được topic.'); }
    finally { setDetailLoading(false); }
  }

  const visibleTopics = useMemo(() => topics.filter(topic => topic.title.toLowerCase().includes(query.toLowerCase())), [topics, query]);
  const prompt = selected ? `Tiếp tục luyện phỏng vấn về chủ đề “${selected.title}”. Hãy hỏi từng câu một, chờ tôi trả lời rồi đánh giá điểm đúng, thiếu và đào sâu như một interviewer Backend Mid–Senior. Chỉ dùng nội dung tôi cung cấp từ Notion; không tự nhận đã lưu review.` : '';

  async function copyHandoff() {
    await navigator.clipboard.writeText(prompt);
    setCopied(true); window.setTimeout(() => setCopied(false), 1800);
  }

  return <div className="app-shell">
    <aside className="sidebar">
      <button className="brand" onClick={() => setView('roadmap')}><span className="brand-mark">B</span><span>Backend<span className="muted">Learning</span></span></button>
      <div className="nav-label">KHÔNG GIAN HỌC</div>
      <button className={`nav-item ${view === 'roadmap' || view === 'topic' ? 'active' : ''}`} onClick={() => setView('roadmap')}><span>▦</span> Kiến thức sâu <span className="nav-count">{topics.length || '—'}</span></button>
      <button className={`nav-item ${view === 'quick-questions' ? 'active' : ''}`} onClick={() => { setError(''); setView('quick-questions'); }}><span>?</span> Câu hỏi PV nhanh</button>
      <button className={`nav-item ${view === 'reviews' ? 'active' : ''}`} onClick={() => { setError(''); setView('reviews'); }}><span>◷</span> Lịch sử review</button>
      <div className="sidebar-bottom">
        <div className="local-pill"><span className="pulse" /> Đang chạy trên máy này</div>
        <span className="sidebar-caption">Nguồn đọc: Notion · cập nhật theo cache ngắn</span>
      </div>
    </aside>

    <main className="main-content">
      <header className="topbar"><div className="breadcrumbs"><span>HỌC TẬP</span><b>/</b><strong>{view === 'reviews' ? 'Lịch sử review' : view === 'quick-questions' ? 'Câu hỏi PV nhanh' : view === 'topic' ? selected?.title : 'Kiến thức sâu'}</strong></div><div className="local-indicator"><span className="pulse" /> LOCAL</div></header>
      <div className="content-wrap">
        {view === 'roadmap' && <>
          <section className="hero">
            <div><div className="eyebrow"><span className="eyebrow-line" /> LỘ TRÌNH CỦA BẠN</div><h1>Kiến thức sâu<span className="period">.</span></h1><p className="hero-copy">Một bản đồ học tập duy nhất, lấy trực tiếp từ Notion của bạn.</p></div>
            <div className="hero-symbol">⌘</div>
          </section>
          <section className="stats-grid">
            <Stat label="CHỦ ĐỀ" value={progress ? `${progress.topics}` : '—'} detail="theo mục lục Notion" />
            <Stat label="MỤC ĐÃ HOÀN THÀNH" value={progress?.totalItems ? `${progress.completedItems}/${progress.totalItems}` : '—'} detail={progress?.state.startsWith('partial') ? `${progress.readableTopics}/${progress.topics} topic đọc được; số liệu chưa đầy đủ` : progress?.totalItems ? 'checkbox trong Notion' : 'chưa có dữ liệu trạng thái'} />
            <Stat label="TIẾN ĐỘ" value={progress?.percent != null ? `${progress.percent}%` : '—'} detail={progress?.percent != null ? 'tính từ checklist hiện có' : 'không tự ước lượng'} />
          </section>
          <div className="section-heading"><div><div className="eyebrow">THƯ VIỆN</div><h2>Chọn chủ đề</h2></div><label className="search-box"><span>⌕</span><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Tìm chủ đề..." /></label></div>
          {loading ? <div className="state-card">Đang đọc mục lục Notion…</div> : error ? <div className="state-card error-card"><b>Không tải được mục lục</b><p>{error}</p><small>Kiểm tra NOTION_API_TOKEN và quyền đọc page gốc trên backend.</small></div> : <div className="topic-grid">{visibleTopics.map(topic => <button className="topic-card" key={topic.id} onClick={() => void openTopic(topic)}><div className="topic-card-top"><span className="topic-index">{String(topic.position).padStart(2, '0')}</span><span className="arrow">↗</span></div><h3>{topic.title}</h3><span className="topic-link">MỞ CHỦ ĐỀ <span>→</span></span></button>)}</div>}
        </>}

        {view === 'topic' && <>
          <button className="back-link" onClick={() => { setView('roadmap'); setError(''); }}>← Tất cả chủ đề</button>
          <section className="topic-header"><div className="eyebrow">CHỦ ĐỀ {String(selected?.position ?? 0).padStart(2, '0')}</div><h1>{selected?.title}<span className="period">.</span></h1><p>Nội dung được đọc trực tiếp từ page Notion này.</p></section>
          <section className="handoff-card"><div><div className="eyebrow">LUYỆN TẬP</div><h3>Tiếp tục với interviewer</h3><p>Sao chép lời nhắc, dán vào ChatGPT để luyện. Sau review, lưu ghi chú vào Notion theo quy trình hiện tại rồi tải lại app.</p></div><button className="secondary-button" onClick={() => void copyHandoff()}>{copied ? 'Đã sao chép ✓' : 'Sao chép lời nhắc'}</button></section>
          {detailLoading && <div className="state-card">Đang tải nội dung topic…</div>}
          {error && <div className="state-card error-card"><b>Không đọc được page này</b><p>{error}</p><small>Page vẫn giữ nguyên trong mục lục; kiểm tra quyền share của integration Notion.</small></div>}
          {page && !page.isComplete && <div className="state-card error-card"><b>Nội dung được tải một phần</b><p>Đã chạm giới hạn an toàn khi đọc cây block. Tiến độ phần trăm không được tính từ dữ liệu này.</p></div>}
          {page && <div className="lesson-layout"><nav className="lesson-toc"><span>TRONG CHỦ ĐỀ</span>{collectHeadings(page.content).map(heading => <a key={heading.id} href={`#notion-${heading.id}`}>{heading.text}</a>)}</nav><article className="lesson-content"><ContentList blocks={page.content} /></article></div>}
        </>}

        {view === 'quick-questions' && <QuickQuestionsPanel />}
        {view === 'reviews' && <>
          <section className="topic-header"><div className="eyebrow">LỊCH SỬ ÔN TẬP</div><h1>Review<span className="period">.</span></h1><p>Các ghi chú có ngày hoặc dấu hiệu review được nhận diện trong nội dung Notion.</p></section>
          {loading ? <div className="state-card">Đang tải review…</div> : error ? <div className="state-card error-card"><b>Không tải được review</b><p>{error}</p></div> : reviews.length === 0 ? <div className="state-card"><b>Chưa tìm thấy ghi chú review</b><p>App không tạo lịch sử giả khi Notion chưa có ghi chú phù hợp.</p></div> : <div className="review-list">{reviews.map(review => <div className="review-row" key={`${review.topicId}-${review.id}`}><span className="review-date">{review.date ?? 'CHƯA CÓ NGÀY'}</span><div><b>{review.topicTitle}</b><p>{review.note}</p></div></div>)}</div>}
        </>}
      </div>
    </main>
  </div>;
}

function QuickQuestionsPanel() {
  const categories = ['Fundamentals', 'Linux & Networking', 'SQL & Database', 'NoSQL & Redis', 'C#/.NET', 'PHP/Laravel', 'Architecture & System Design', 'Testing & CI/CD', 'Security', 'Production & Observability', 'Soft Skills'];
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('');
  const [difficulty, setDifficulty] = useState('');
  const [status, setStatus] = useState('');
  const [result, setResult] = useState<QuickQuestionResults>({ items: [], nextCursor: null, hasMore: false });
  const [loading, setLoading] = useState(true);
  const [moreLoading, setMoreLoading] = useState(false);
  const [error, setError] = useState('');
  const [detail, setDetail] = useState<QuickQuestionDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    let active = true;
    const timer = window.setTimeout(() => {
      setLoading(true); setError(''); setResult({ items: [], nextCursor: null, hasMore: false }); setDetail(null);
      api.quickQuestions({ search, category, difficulty, status })
        .then(value => { if (active) setResult(value); })
        .catch((reason: unknown) => { if (active) setError(reason instanceof Error ? reason.message : 'Không tải được câu hỏi.'); })
        .finally(() => { if (active) setLoading(false); });
    }, 220);
    return () => { active = false; window.clearTimeout(timer); };
  }, [search, category, difficulty, status]);

  async function openQuestion(question: QuickQuestion) {
    setDetail(null); setDetailLoading(true); setError('');
    try { setDetail(await api.quickQuestion(question.id)); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Không tải được nội dung câu hỏi.'); }
    finally { setDetailLoading(false); }
  }

  async function loadMore() {
    if (!result.nextCursor) return;
    setMoreLoading(true);
    try {
      const next = await api.quickQuestions({ search, category, difficulty, status, cursor: result.nextCursor });
      setResult(current => ({ ...next, items: [...current.items, ...next.items] }));
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Không tải thêm được câu hỏi.'); }
    finally { setMoreLoading(false); }
  }

  return <>
    <section className="topic-header"><div className="eyebrow">NGÂN HÀNG CÂU HỎI</div><h1>Câu hỏi PV nhanh<span className="period">.</span></h1><p>Danh sách câu hỏi đọc trực tiếp từ Notion, có thể tìm theo nội dung và lọc theo nhóm.</p></section>
    {detail ? <>
      <button className="back-link" onClick={() => setDetail(null)}>← Tất cả câu hỏi</button>
      <section className="topic-header quick-question-detail-header"><div className="eyebrow">{detail.question.category || 'CÂU HỎI'} {detail.question.difficulty && `· ${detail.question.difficulty}`}</div><h1>{detail.question.title}</h1><p>{[detail.question.topic, detail.question.answerStatus].filter(Boolean).join(' · ')}</p></section>
      {detail.question.standardQuestion && detail.question.standardQuestion !== detail.question.title && <div className="quick-standard"><b>Câu chuẩn</b><p>{detail.question.standardQuestion}</p></div>}
      {!detail.isComplete && <div className="state-card">Nội dung được tải một phần do giới hạn đọc an toàn.</div>}
      <article className="lesson-content quick-answer-content"><ContentList blocks={detail.content} /></article>
      {detail.question.url && <a className="quick-source-link" href={detail.question.url} target="_blank" rel="noreferrer">Mở page gốc trong Notion ↗</a>}
    </> : <>
      <div className="quick-question-filters">
        <label className="search-box"><span>⌕</span><input value={search} onChange={event => setSearch(event.target.value)} placeholder="Tìm câu hỏi hoặc chủ đề..." /></label>
        <select value={category} onChange={event => setCategory(event.target.value)}><option value="">Tất cả nhóm</option>{categories.map(item => <option key={item}>{item}</option>)}</select>
        <select value={difficulty} onChange={event => setDifficulty(event.target.value)}><option value="">Mọi độ khó</option>{['Junior', 'Middle', 'Senior'].map(item => <option key={item}>{item}</option>)}</select>
        <select value={status} onChange={event => setStatus(event.target.value)}><option value="">Mọi trạng thái</option>{['Chưa xử lý', 'Đã chuẩn hóa', 'Câu chuẩn', 'Trùng/gộp'].map(item => <option key={item}>{item}</option>)}</select>
      </div>
      {error && <div className="state-card error-card"><b>Không tải được câu hỏi</b><p>{error}</p><small>Kiểm tra quyền đọc data source ABC của integration Notion.</small></div>}
      {loading ? <div className="state-card">Đang đọc câu hỏi từ Notion…</div> : !error && result.items.length === 0 ? <div className="state-card">Không tìm thấy câu hỏi phù hợp.</div> : <>
        <div className="quick-result-count">{result.items.length} câu hỏi đã tải{result.hasMore ? ' · còn kết quả' : ''}</div>
        <div className="quick-question-list">{result.items.map((question, index) => <button className="quick-question-card" key={question.id} onClick={() => void openQuestion(question)}>
          <span className="quick-question-number">{String(index + 1).padStart(2, '0')}</span><span className="quick-question-main"><b>{question.title}</b><span>{[question.category, question.topic].filter(Boolean).join(' · ')}</span></span>
          <span className="quick-question-badges">{question.difficulty && <i>{question.difficulty}</i>}{question.answerStatus && <i>{question.answerStatus}</i>}</span><span className="arrow">↗</span>
        </button>)}</div>
        {result.hasMore && <button className="secondary-button quick-more-button" disabled={moreLoading} onClick={() => void loadMore()}>{moreLoading ? 'Đang tải…' : 'Tải thêm câu hỏi'}</button>}
      </>}
      {detailLoading && <div className="state-card">Đang tải nội dung câu hỏi…</div>}
    </>}
  </>;
}

function Stat({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <div className="stat-card"><span>{label}</span><strong>{value}</strong><small>{detail}</small></div>;
}

function ContentList({ blocks }: { blocks: ContentBlock[] }) {
  const visibleBlocks = blocks.filter(block => block.text.trim() || block.children.length > 0 || block.url || block.type === 'divider');
  return <div className="content-list">{visibleBlocks.map(block => <ContentItem key={block.id} block={block} />)}</div>;
}

function ContentItem({ block }: { block: ContentBlock }) {
  const heading = block.type.startsWith('heading_');
  const level = Number(block.type.at(-1)) || 2;
  const Tag = heading ? (`h${level}` as 'h1' | 'h2' | 'h3') : 'div';
  const diagram = block.type === 'paragraph' && /[├└│┌┬┴┼─]{2,}|\s\|\s|→|←/.test(block.text);
  if (block.type === 'divider') return <hr className="content-divider" />;
  if (block.type === 'code' || diagram) return <div className={`content-block ${diagram ? 'block-diagram' : 'block-code'}`}>
    {block.type === 'code' ? <div className="code-frame">{block.language && <span className="code-language">{block.language}</span>}<SyntaxHighlightedCode text={block.text} language={block.language} /></div> : <pre><code>{block.text}</code></pre>}
    {block.children.length > 0 && <div className="nested-blocks"><ContentList blocks={block.children} /></div>}
  </div>;
  return <div className={`content-block block-${block.type}`}>
    <div className="block-line">
      {block.checked !== null && block.checked !== undefined && <span className={`checkbox ${block.checked ? 'checked' : ''}`}>{block.checked ? '✓' : ''}</span>}
      {block.text && (block.type === 'quote'
        ? <blockquote className="block-quote-text">{block.text}</blockquote>
        : <Tag id={heading ? `notion-${block.id}` : undefined} className={heading ? 'notion-heading' : 'block-text'}>{block.text}</Tag>)}
      {block.url && <a className="inline-link" href={block.url} target="_blank" rel="noreferrer">Mở liên kết ↗</a>}
    </div>
    {block.children.length > 0 && <div className="nested-blocks"><ContentList blocks={block.children} /></div>}
  </div>;
}

function SyntaxHighlightedCode({ text, language }: { text: string; language?: string | null }) {
  const lang = (language ?? '').toLowerCase();
  const keywords = lang.includes('sql')
    ? new Set('select from where insert into values update set delete create table join left right inner outer on as group by order having null is not and or distinct count'.split(' '))
    : lang.includes('python')
      ? new Set('def class return if elif else for while in try except finally import from as with pass raise yield async await lambda and or not is None True False'.split(' '))
      : new Set('abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while record required init var Task ValueTask'.split(' '));
  const tokenPattern = /(\s+|\/\/[^\r\n]*|\/\*[\s\S]*?\*\/|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`|\b[A-Za-z_$][\w$]*\b|\b\d+(?:\.\d+)?\b|[{}()[\];,.]|[=+\-*\/%<>!&|^~?:])/g;
  const tokens = text.match(tokenPattern) ?? [text];
  let offset = 0;
  let previousWord = '';
  return <pre><code>{tokens.map((token, index) => {
    const nextText = text.slice(offset + token.length);
    let kind = '';
    if (/^\s+$/.test(token)) kind = '';
    else if (token.startsWith('//') || token.startsWith('/*')) kind = 'comment';
    else if (/^["'`]/.test(token)) kind = 'string';
    else if (/^\d/.test(token)) kind = 'number';
    else if (/^[A-Za-z_$]/.test(token)) {
      if (keywords.has(token) || (lang.includes('sql') && keywords.has(token.toLowerCase()))) kind = 'keyword';
      else if (['int', 'string', 'bool', 'decimal', 'double', 'float', 'long', 'short', 'char', 'byte', 'object', 'void', 'Task', 'ValueTask'].includes(token)) kind = 'type';
      else if (['class', 'struct', 'interface', 'record', 'enum', 'new'].includes(previousWord) || /^[A-Z]/.test(token)) kind = 'type';
      else if (nextText.trimStart().startsWith('(')) kind = 'function';
      else kind = 'variable';
      previousWord = token;
    } else if (/^[{}()[\];,.]$/.test(token)) kind = 'punctuation';
    else kind = 'operator';
    offset += token.length;
    return kind ? <span className={`syntax-${kind}`} key={index}>{token}</span> : token;
  })}</code></pre>;
}

function collectHeadings(blocks: ContentBlock[]): ContentBlock[] {
  return blocks.flatMap(block => [
    ...(block.type.startsWith('heading_') && block.text ? [block] : []),
    ...collectHeadings(block.children),
  ]);
}

export default App;
