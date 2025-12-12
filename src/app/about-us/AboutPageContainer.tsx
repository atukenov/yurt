'use client';

import { useState } from 'react';

import Lottie from '#components/base/Lottie';
import { getAnimSrc } from '#utils/constants/common';
import FooterSection from '../_homepage/FooterSection';
import Navbar from './Navbar';

import './aboutPage.scss';

export default function AboutPageContainer() {
	const [menuOpen, setMenuOpen] = useState(false);

	return (
		<div className='aboutPage'>
			<Navbar menuOpen={menuOpen} setMenuOpen={setMenuOpen} />
			<div className={`aboutPageSections ${menuOpen ? 'menuOpen' : ''}`}>
				<section className="aboutUsSection">
					<div className="aboutUsContent">
						<h2>About Us</h2>
						<div className="contentText">
							<p>
								We are a team of highly motivated revolutionaries, dedicated towards
								revolutionizing the restaurant industry by transforming the way your
								customers order at your restaurant - by going contactless and
								paperless.
							</p>
							<p>
								It&apos;s time to bridge the gap between your customers and your
								kitchen, in an efficient, proficient and affordable way, with us.
							</p>
							<p>
								Our mission is to empower restaurant owners with cutting-edge technology
								that simplifies operations, reduces costs, and enhances the dining
								experience for customers. We believe in eliminating third-party
								dependencies and putting control back in the hands of restaurant owners.
							</p>
						</div>
						<div className="missionVision">
							<div className="missionItem">
								<h3>Our Mission</h3>
								<p>
									To transform the restaurant industry through innovative, contactless
									ordering solutions that bring customers and kitchens closer together.
								</p>
							</div>
							<div className="missionItem">
								<h3>Our Vision</h3>
								<p>
									A world where every restaurant, big or small, has access to
									professional-grade technology that enhances both operations and
									customer satisfaction.
								</p>
							</div>
						</div>
					</div>
					<div className="aboutUsAnim">
						<Lottie
							className="scanMenuAnim"
							src={getAnimSrc('FoodScanMenu')}
							speed={0.8}
						/>
					</div>
				</section>
				<FooterSection />
			</div>
		</div>
	);
}
