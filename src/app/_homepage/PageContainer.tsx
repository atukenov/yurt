'use client';

import { useState } from 'react';

import AboutSection from './AboutSection';
import CustomerLoginSection from './CustomerLoginSection';
import FeatureSection from './FeatureSection';
import FooterSection from './FooterSection';
import LandingSection from './LandingSection';
import LoginSection from './LoginSection';
import Navbar from './Navbar';

import './pageContainer.scss';

export default function PageContainer () {
	const [menuOpen, setMenuOpen] = useState(false);

	return (
		<>
			<div className='homepage'>
				<Navbar menuOpen={menuOpen} setMenuOpen={setMenuOpen} />
				<div
					className={`homepageSections ${menuOpen ? 'menuOpen' : ''}`}
				>
					{/* <LandingSection /> */}
					{/* <AboutSection /> */}
					{/* <FeatureSection /> */}
					<CustomerLoginSection />
					{/* <LoginSection /> */}
					<FooterSection />
				</div>
			</div>
		</>
	);
}
