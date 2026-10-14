import { useState } from 'react'
import { ArrowRight, BellRing, BookOpenCheck, CalendarCheck2, Camera, ChefHat, ClipboardCheck, Heart, Leaf, ShieldCheck, Sparkles, UsersRound } from 'lucide-react'

const steps = [
  { label: 'Báo vắng', title: 'Phụ huynh gửi thông tin báo vắng', detail: 'Chọn một ngày hoặc một khoảng thời gian. Thông tin được cập nhật vào danh sách tổng hợp trước giờ chốt.', icon: BellRing, color: 'peach' },
  { label: 'Tổng hợp suất', title: 'Rà soát và chốt suất theo lớp', detail: 'Số suất được tổng hợp từ ghi danh, báo vắng và ngoại lệ. Quản lý rà soát số liệu làm căn cứ chuẩn bị.', icon: ClipboardCheck, color: 'violet' },
  { label: 'Bếp chuẩn bị', title: 'Nhà bếp tra cứu số liệu đã chốt', detail: 'Nhà bếp xem danh sách theo lớp và các điều chỉnh đã được duyệt.', icon: ChefHat, color: 'yellow' },
  { label: 'Tra cứu', title: 'Tra cứu thông tin bữa ăn', detail: 'Thực đơn và thông tin bữa ăn được chia sẻ theo dữ liệu nhà trường công bố.', icon: Heart, color: 'mint' },
] as const

const features = [
  { icon: CalendarCheck2, title: 'Quản lý lịch ăn', body: 'Thiết lập lịch theo năm học và theo dõi các ngày hoạt động, ngày nghỉ.', color: 'peach' },
  { icon: UsersRound, title: 'Quản lý danh sách trẻ', body: 'Theo dõi lớp, hồ sơ trẻ và thông tin ghi danh theo năm học.', color: 'yellow' },
  { icon: ClipboardCheck, title: 'Chốt và điều chỉnh số suất', body: 'Tổng hợp theo lớp, lưu bản chốt và ghi nhận các điều chỉnh đã được duyệt.', color: 'violet' },
  { icon: Camera, title: 'Ghi nhận bữa ăn', body: 'Lưu thực đơn, hình ảnh và thông tin thực tế để tra cứu theo quyền.', color: 'mint' },
] as const

export function LandingPage({ onLogin }: Readonly<{ onLogin: () => void }>) {
  const [activeStep, setActiveStep] = useState(0)
  const ActiveIcon = steps[activeStep].icon

  return <div className="landing">
    <div className="landing-orb landing-orb-one" aria-hidden="true" />
    <div className="landing-orb landing-orb-two" aria-hidden="true" />
    <header className="landing-header">
      <a className="landing-brand" href="#top" aria-label="MealTrace, về đầu trang"><span className="landing-brand-icon"><Leaf size={22} strokeWidth={2.8} /></span><span>meal<span>trace</span></span></a>
      <nav aria-label="Điều hướng trang giới thiệu"><a href="#giai-phap">Nghiệp vụ</a><a href="#quy-trinh">Quy trình</a><a href="#vai-tro">Người dùng</a></nav>
      <button type="button" className="clay-button clay-button-small" onClick={onLogin}>Đăng nhập <ArrowRight size={17} /></button>
    </header>

    <main id="top">
      <section className="landing-hero" aria-labelledby="hero-title">
        <div className="hero-copy">
          <span className="landing-kicker"><Sparkles size={17} /> HỆ THỐNG QUẢN LÝ BỮA ĂN BÁN TRÚ</span>
          <h1 id="hero-title">Quản lý bữa ăn bán trú,<br /><em>theo dõi tập trung.</em></h1>
          <p>MealTrace hỗ trợ cán bộ quản lý và giáo viên theo dõi lịch ăn, danh sách trẻ, số suất, báo vắng và các điều chỉnh. Thông tin được lưu theo từng ngày ăn để thuận tiện tra cứu.</p>
          <div className="hero-actions"><button type="button" className="clay-button" onClick={onLogin}>Đăng nhập hệ thống <ArrowRight size={20} /></button><a className="clay-button clay-button-outline" href="#quy-trinh">Xem quy trình</a></div>
          <div className="hero-mini"><span className="hero-mini-icon"><ShieldCheck size={18} /></span><span>Phân quyền theo vai trò · Lưu lịch sử xử lý</span></div>
        </div>
        <div className="hero-art" aria-label="Bản xem trước minh họa quy trình bữa ăn">
          <div className="hero-art-spark hero-art-spark-left" aria-hidden="true">✦</div><div className="hero-art-spark hero-art-spark-right" aria-hidden="true">✳</div>
          <div className="hero-card hero-card-menu">
            <div className="hero-card-top"><span className="hero-card-icon"><ChefHat size={21} /></span><span className="hero-card-tag">MINH HỌA GIAO DIỆN</span></div>
            <strong>Thực đơn hôm nay</strong><small>Bữa trưa · Lớp Mầm</small>
            <div className="food-plate" aria-hidden="true"><span className="food-rice" /><span className="food-carrot" /><span className="food-green" /><span className="food-egg" /></div>
            <div className="hero-food-name">Cơm · rau củ · món chính</div>
          </div>
          <div className="hero-card hero-card-absence"><span className="mini-icon peach"><CalendarCheck2 size={20} /></span><span><strong>Thông tin báo vắng</strong><small>Theo ngày hoặc khoảng thời gian</small></span><span className="mini-check">✓</span></div>
          <div className="hero-card hero-card-count"><span className="mini-icon mint"><UsersRound size={20} /></span><span><strong>Tổng hợp số suất</strong><small>Theo lớp · Có lịch sử chốt</small></span><span className="count-dots" aria-hidden="true">● ● ●</span></div>
        </div>
      </section>

      <section className="landing-section" id="giai-phap" aria-labelledby="solution-title">
        <div className="section-heading"><span className="landing-kicker">NGHIỆP VỤ BỮA ĂN</span><h2 id="solution-title">Theo dõi công việc<br />trên cùng hệ thống.</h2><p>Lịch ăn, danh sách suất và các điều chỉnh được quản lý theo từng ngày ăn.</p></div>
        <div className="feature-grid">{features.map(({ icon: Icon, title, body, color }) => <article className={`feature-card clay-${color}`} key={title}><span className="feature-icon"><Icon size={27} strokeWidth={2.3} /></span><h3>{title}</h3><p>{body}</p><span className="feature-mark" aria-hidden="true">✦</span></article>)}</div>
      </section>

      <section className="landing-section journey-section" id="quy-trinh" aria-labelledby="journey-title">
        <div className="section-heading"><span className="landing-kicker">QUY TRÌNH XỬ LÝ</span><h2 id="journey-title">Từ báo vắng đến<br />danh sách suất ăn.</h2><p>Các bước phối hợp giữa phụ huynh, giáo viên, cán bộ quản lý và nhà bếp.</p></div>
        <div className="journey-layout"><div className="journey-list" role="tablist" aria-label="Các bước trong quy trình">{steps.map((step, index) => <button key={step.label} type="button" role="tab" aria-selected={activeStep === index} className={`journey-step ${activeStep === index ? 'selected' : ''}`} onClick={() => setActiveStep(index)}><span>{String(index + 1).padStart(2, '0')}</span><strong>{step.label}</strong><ArrowRight size={19} /></button>)}</div>
          <div className={`journey-preview clay-${steps[activeStep].color}`} role="tabpanel"><div className="journey-preview-top"><span>MÔ PHỎNG QUY TRÌNH</span><span>BƯỚC {activeStep + 1}/4</span></div><div className="journey-progress" aria-label={`Bước ${activeStep + 1} trên 4`}><span style={{ width: `${(activeStep + 1) * 25}%` }} /></div><span className="journey-big-icon"><ActiveIcon size={46} strokeWidth={1.9} /></span><h3>{steps[activeStep].title}</h3><p>{steps[activeStep].detail}</p><div className="journey-stickers" aria-hidden="true"><span>✳</span><span>●</span><span>✦</span></div></div></div>
      </section>

      <section className="landing-section roles-section" id="vai-tro" aria-labelledby="roles-title"><div className="section-heading"><span className="landing-kicker">VAI TRÒ SỬ DỤNG</span><h2 id="roles-title">Nghiệp vụ phù hợp<br />với từng vai trò.</h2><p>Quyền truy cập và thông tin hiển thị được phân theo nhiệm vụ tại nhà trường.</p></div><div className="role-grid"><article><span className="role-avatar role-teacher"><ShieldCheck size={25} /></span><span className="role-label">QUẢN LÝ NHÀ TRƯỜNG</span><h3>Điều phối hoạt động bữa ăn</h3><p>Thiết lập lịch ăn, quản lý tài khoản, rà soát số suất và duyệt các đề nghị điều chỉnh.</p></article><article><span className="role-avatar role-teacher"><BookOpenCheck size={25} /></span><span className="role-label">GIÁO VIÊN</span><h3>Theo dõi trẻ trong lớp</h3><p>Quản lý danh sách trẻ, theo dõi số suất và phối hợp cập nhật thông tin vắng.</p></article><article><span className="role-avatar role-kitchen"><ChefHat size={25} /></span><span className="role-label">NHÂN VIÊN BẾP</span><h3>Tra cứu số liệu chuẩn bị</h3><p>Xem số suất đã chốt theo ngày, lớp và các điều chỉnh được duyệt.</p></article><article><span className="role-avatar role-parent"><Heart size={25} /></span><span className="role-label">PHỤ HUYNH</span><h3>Cập nhật thông tin của trẻ</h3><p>Gửi thông tin báo vắng và xem thực đơn cùng thông tin bữa ăn được nhà trường công bố.</p></article></div></section>

      <section className="landing-cta"><div className="cta-decoration" aria-hidden="true">✳</div><div><span className="landing-kicker">TRUY CẬP HỆ THỐNG</span><h2>Quản lý công việc<br />trên MealTrace.</h2><p>Đăng nhập bằng tài khoản do nhà trường cấp.</p></div><button type="button" className="clay-button clay-button-light" onClick={onLogin}>Đăng nhập hệ thống <ArrowRight size={20} /></button></section>
    </main>
    <footer className="landing-footer"><span className="landing-brand"><span className="landing-brand-icon"><Leaf size={19} /></span><span>meal<span>trace</span></span></span><span>Hệ thống quản lý bữa ăn bán trú.</span><a href="#top">Về đầu trang ↑</a></footer>
  </div>
}
