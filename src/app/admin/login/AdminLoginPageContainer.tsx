'use client';

import { useState } from 'react';

import Lottie from '#components/base/Lottie';
import { getAnimSrc } from '#utils/constants/common';
import LoginSection from '../../_homepage/LoginSection';
import FooterSection from '../../_homepage/FooterSection';
import Navbar from './Navbar';

import './adminLoginPage.scss';

export default function AdminLoginPageContainer() {
	const [menuOpen, setMenuOpen] = useState(false);

	return (
		<div className='adminLoginPage'>
			<Navbar menuOpen={menuOpen} setMenuOpen={setMenuOpen} />
			<div className={`adminLoginSections ${menuOpen ? 'menuOpen' : ''}`}>
				<section className="adminLoginHero">
					<LoginSection />
				</section>
				<FooterSection />
			</div>
		</div>
	);
}
