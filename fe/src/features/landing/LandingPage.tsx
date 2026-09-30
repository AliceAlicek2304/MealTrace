import { useState } from 'react'
import { ArrowRight, BellRing, BookOpenCheck, CalendarCheck2, Camera, ChefHat, ClipboardCheck, Heart, Leaf, ShieldCheck, Sparkles, UsersRound } from 'lucide-react'

const steps = [
  { label: 'Báo vắng', title: 'Phụ huynh báo vắng thật nhanh', detail: 'Chọn một ngày hoặc cả khoảng thời gian. Trẻ còn lại được mặc định dự kiến ăn.', icon: BellRing, color: 'peach' },
  { label: 'Chốt suất', title: 'Nhà trường có danh sách rõ ràng', detail: 'Số suất được tổng hợp theo lớp và giữ lại bản chốt đã gửi bếp.', icon: ClipboardCheck, color: 'violet' },
  { label: 'Bếp chuẩn bị', title: 'Bếp nhận đúng số suất', detail: 'Mọi người cùng theo dõi một phiên bản số liệu cho ngày ăn.', icon: ChefHat, color: 'yellow' },
  { label: 'Theo dõi', title: 'Phụ huynh yên tâm hơn', detail: 'Thực đơn và thông tin bữa ăn được công bố theo đúng trẻ được liên kết.', icon: Heart, color: 'mint' },
] as const

const features = [
  { icon: CalendarCheck2, title: 'Báo vắng linh hoạt', body: 'Một ngày hoặc dài hạn, không cần điểm danh lại toàn bộ trẻ mỗi sáng.', color: 'peach' },
  { icon: ChefHat, title: 'Thực đơn dễ theo dõi', body: 'Bếp, nhà trường và phụ huynh nhìn thấy thông tin phù hợp với vai trò.', color: 'yellow' },
  { icon: Camera, title: 'Ghi lại bữa ăn', body: 'Ảnh món ăn và thông tin thực tế giúp bữa ăn có thêm minh chứng.', color: 'violet' },
  { icon: ShieldCheck, title: 'Dữ liệu có nguồn', body: 'Từ số suất đến báo cáo, từng bước được lưu để đối chiếu khi cần.', color: 'mint' },
] as const

export function LandingPage({ onLogin }: { onLogin: () => void }) {
  const [activeStep, setActiveStep] = useState(0)
  const ActiveIcon = steps[activeStep].icon

  return <div className="landing">
    <div className="landing-orb landing-orb-one" aria-hidden="true" />
    <div className="landing-orb landing-orb-two" aria-hidden="true" />
    <header className="landing-header">
      <a className="landing-brand" href="#top" aria-label="MealTrace, về đầu trang"><span className="landing-brand-icon"><Leaf size={22} strokeWidth={2.8} /></span><span>meal<span>trace</span></span></a>
      <nav aria-label="Điều hướng trang giới thiệu"><a href="#giai-phap">Giải pháp</a><a href="#quy-trinh">Quy trình</a><a href="#vai-tro">Dành cho ai?</a></nav>
      <button type="button" className="clay-button clay-button-small" onClick={onLogin}>Đăng nhập <ArrowRight size={17} /></button>
    </header>

    <main id="top">
      <section className="landing-hero" aria-labelledby="hero-title">
        <div className="hero-copy">
          <span className="landing-kicker"><Sparkles size={17} /> MỖI BỮA ĂN, MỘT CHÚT YÊN TÂM</span>
          <h1 id="hero-title">Bữa ăn mỗi ngày,<br /><em>yên tâm từng bước.</em></h1>
          <p>MealTrace kết nối phụ huynh, giáo viên, nhà trường và bếp trong một quy trình bữa ăn bán trú dễ theo dõi, rõ số liệu và thân thiện hơn mỗi ngày.</p>
          <div className="hero-actions"><button type="button" className="clay-button" onClick={onLogin}>Vào MealTrace <ArrowRight size={20} /></button><a className="clay-button clay-button-outline" href="#quy-trinh">Khám phá quy trình</a></div>
          <div className="hero-mini"><span className="hero-mini-icon"><Heart size={18} fill="currentColor" /></span><span>Cho một ngày học vui và bữa ăn được chăm chút.</span></div>
        </div>
        <div className="hero-art" aria-label="Bản xem trước minh họa quy trình bữa ăn">
          <div className="hero-art-spark hero-art-spark-left" aria-hidden="true">✦</div><div className="hero-art-spark hero-art-spark-right" aria-hidden="true">✳</div>
          <div className="hero-card hero-card-menu">
            <div className="hero-card-top"><span className="hero-card-icon"><ChefHat size={21} /></span><span className="hero-card-tag">XEM TRƯỚC MINH HỌA</span></div>
            <strong>Thực đơn hôm nay</strong><small>Bữa trưa · Lớp Mầm</small>
            <div className="food-plate" aria-hidden="true"><span className="food-rice" /><span className="food-carrot" /><span className="food-green" /><span className="food-egg" /></div>
            <div className="hero-food-name">Cơm · rau củ · món chính</div>
          </div>
          <div className="hero-card hero-card-absence"><span className="mini-icon peach"><CalendarCheck2 size={20} /></span><span><strong>Báo vắng thật gọn</strong><small>Một ngày hoặc nhiều ngày</small></span><span className="mini-check">✓</span></div>
          <div className="hero-card hero-card-count"><span className="mini-icon mint"><UsersRound size={20} /></span><span><strong>Số suất rõ ràng</strong><small>Theo lớp · Có bản chốt</small></span><span className="count-dots" aria-hidden="true">● ● ●</span></div>
        </div>
      </section>

      <section className="landing-section" id="giai-phap" aria-labelledby="solution-title">
        <div className="section-heading"><span className="landing-kicker">GIẢI PHÁP NHẸ NHÀNG HƠN</span><h2 id="solution-title">Những việc nhỏ, chăm sóc <em>thật lớn.</em></h2><p>Mỗi vai trò có đúng thông tin cần dùng, từ đầu ngày ăn đến lúc đối chiếu báo cáo.</p></div>
        <div className="feature-grid">{features.map(({ icon: Icon, title, body, color }) => <article className={`feature-card clay-${color}`} key={title}><span className="feature-icon"><Icon size={27} strokeWidth={2.3} /></span><h3>{title}</h3><p>{body}</p><span className="feature-mark" aria-hidden="true">✦</span></article>)}</div>
      </section>

      <section className="landing-section journey-section" id="quy-trinh" aria-labelledby="journey-title">
        <div className="section-heading"><span className="landing-kicker">MỘT NGÀY ĂN, BỐN BƯỚC</span><h2 id="journey-title">Theo dõi tiến trình <em>thật dễ.</em></h2><p>Chạm vào từng bước để xem một ngày bữa ăn được phối hợp như thế nào.</p></div>
        <div className="journey-layout"><div className="journey-list" role="tablist" aria-label="Các bước của bữa ăn">{steps.map((step, index) => <button key={step.label} type="button" role="tab" aria-selected={activeStep === index} className={`journey-step ${activeStep === index ? 'selected' : ''}`} onClick={() => setActiveStep(index)}><span>{String(index + 1).padStart(2, '0')}</span><strong>{step.label}</strong><ArrowRight size={19} /></button>)}</div>
          <div className={`journey-preview clay-${steps[activeStep].color}`} role="tabpanel"><div className="journey-preview-top"><span>DEMO QUY TRÌNH</span><span>BƯỚC {activeStep + 1}/4</span></div><div className="journey-progress" aria-label={`Bước ${activeStep + 1} trên 4`}><span style={{ width: `${(activeStep + 1) * 25}%` }} /></div><span className="journey-big-icon"><ActiveIcon size={46} strokeWidth={1.9} /></span><h3>{steps[activeStep].title}</h3><p>{steps[activeStep].detail}</p><div className="journey-stickers" aria-hidden="true"><span>✳</span><span>●</span><span>✦</span></div></div></div>
      </section>

      <section className="landing-section roles-section" id="vai-tro" aria-labelledby="roles-title"><div className="section-heading"><span className="landing-kicker">AI CŨNG CÓ PHẦN VIỆC CỦA MÌNH</span><h2 id="roles-title">Cùng nhau chăm một <em>bữa ăn tốt.</em></h2></div><div className="role-grid"><article><span className="role-avatar role-parent"><Heart size={25} /></span><span className="role-label">PHỤ HUYNH</span><h3>Biết hôm nay con ăn gì.</h3><p>Báo vắng và theo dõi thông tin đã được nhà trường công bố.</p></article><article><span className="role-avatar role-teacher"><BookOpenCheck size={25} /></span><span className="role-label">GIÁO VIÊN</span><h3>Ít thao tác lặp lại hơn.</h3><p>Xem danh sách lớp và ghi những trường hợp khác với dự kiến.</p></article><article><span className="role-avatar role-kitchen"><ChefHat size={25} /></span><span className="role-label">NHÀ BẾP</span><h3>Chuẩn bị với số liệu rõ ràng.</h3><p>Nhận bản chốt suất và theo dõi thông tin phục vụ bữa ăn.</p></article></div></section>

      <section className="landing-cta"><div className="cta-decoration" aria-hidden="true">✳</div><div><span className="landing-kicker">SẴN SÀNG BẮT ĐẦU?</span><h2>Một bữa ăn được chăm chút<br />bắt đầu từ sự kết nối.</h2><p>Đăng nhập bằng tài khoản MealTrace do nhà trường cấp.</p></div><button type="button" className="clay-button clay-button-light" onClick={onLogin}>Đăng nhập ngay <ArrowRight size={20} /></button></section>
    </main>
    <footer className="landing-footer"><span className="landing-brand"><span className="landing-brand-icon"><Leaf size={19} /></span><span>meal<span>trace</span></span></span><span>Chăm bữa ăn, giữ sự yên tâm.</span><a href="#top">Về đầu trang ↑</a></footer>
  </div>
}
