import Lightings from './Lightings';
import './footerSection.scss';

const FooterSection = () => {
	return (
		<section className='footerSection' id='homepage-footer'>
			<p>
				© {new Date().getFullYear()} 💎, Inc. All rights
				reserved.
			</p>
			<Lightings />
		</section>
	);
};

export default FooterSection;
